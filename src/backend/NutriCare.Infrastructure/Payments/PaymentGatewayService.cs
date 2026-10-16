namespace NutriCare.Infrastructure.Payments;

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using NutriCare.Domain.Interfaces;
using NutriCare.Domain.Exceptions;

public class PaymentGatewayService : IPaymentGatewayService, IPaymentRefundReconciliation
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public PaymentGatewayService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IHostEnvironment environment)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _environment = environment;
    }

    public Task<PaymentGatewayInitializeResult> InitializePaymentAsync(
        string provider,
        PaymentGatewayInitializeRequest request,
        CancellationToken cancellationToken = default)
    {
        return NormalizeProvider(provider) switch
        {
            "Stripe" => InitializeStripeAsync(request, cancellationToken),
            "JazzCash" or "EasyPaisa" => throw new InvalidOperationException("Wallet checkout is disabled until the contracted provider API is approved"),
            _ => throw new InvalidOperationException("Unsupported payment provider")
        };
    }

    public Task<PaymentGatewayRefundResult> RefundAsync(
        string provider,
        string providerPaymentId,
        decimal amount,
        string currency,
        string? reason,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null)
    {
        return NormalizeProvider(provider) switch
        {
            "Stripe" => RefundStripeAsync(providerPaymentId, amount, currency, reason, cancellationToken, idempotencyKey),
            "JazzCash" or "EasyPaisa" => throw new InvalidOperationException("Wallet refunds are disabled until the contracted provider API is approved"),
            _ => throw new InvalidOperationException("Unsupported payment provider")
        };
    }

    public Task<PaymentGatewayWebhookResult> ParseWebhookAsync(
        string provider,
        string payload,
        string? signature,
        CancellationToken cancellationToken = default)
    {
        return NormalizeProvider(provider) switch
        {
            "Stripe" => Task.FromResult(ParseStripeWebhook(payload, signature)),
            "JazzCash" or "EasyPaisa" => throw new InvalidOperationException("Wallet callbacks are disabled until the contracted provider API is approved"),
            _ => throw new InvalidOperationException("Unsupported payment provider")
        };
    }

    public async Task<PaymentGatewayRefundState?> FindAsync(string provider, string providerPaymentId, string operationKey,
        string? providerRefundId = null, CancellationToken cancellationToken = default)
    {
        if (NormalizeProvider(provider) != "Stripe") throw new InvalidOperationException("This refund provider is not available");
        var secret = _configuration["Payments:Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("The refund provider is not configured");
        if (!providerPaymentId.StartsWith("pi_", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(operationKey))
            throw new ArgumentException("A verified payment intent and refund operation key are required");
        var client = _httpClientFactory.CreateClient("Stripe");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        if (!string.IsNullOrWhiteSpace(providerRefundId))
        {
            if (!providerRefundId.StartsWith("re_", StringComparison.Ordinal)) throw new ArgumentException("The provider refund identity is invalid");
            using var response = await client.GetAsync("https://api.stripe.com/v1/refunds/" + Uri.EscapeDataString(providerRefundId), cancellationToken);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("The provider refund could not be verified");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return ReadRefundState(json.RootElement);
        }
        string? after = null;
        PaymentGatewayRefundState? match = null;
        for (var page = 0; page < 10; page++)
        {
            var url = "https://api.stripe.com/v1/refunds?payment_intent=" + Uri.EscapeDataString(providerPaymentId) + "&limit=100" +
                (after == null ? "" : "&starting_after=" + Uri.EscapeDataString(after));
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Provider refund reconciliation is unavailable");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = json.RootElement;
            foreach (var item in root.GetProperty("data").EnumerateArray())
            {
                var state = ReadRefundState(item);
                after = state.ProviderRefundId;
                if (state.OperationKey != operationKey) continue;
                if (match != null) throw new InvalidOperationException("Multiple provider refunds have the same operation identity");
                match = state;
            }
            if (!root.GetProperty("has_more").GetBoolean()) return match;
            if (string.IsNullOrWhiteSpace(after)) throw new InvalidOperationException("Provider refund pagination is incomplete");
        }
        throw new InvalidOperationException("Provider refund reconciliation requires manual review");
    }

    private static PaymentGatewayRefundState ReadRefundState(JsonElement refund)
    {
        var currency = ReadString(refund, "currency")?.ToUpperInvariant() ?? throw new InvalidOperationException("Provider refund currency is missing");
        var key = refund.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object ? ReadString(metadata, "nutricare_refund_key") : null;
        return new(ReadString(refund, "id") ?? throw new InvalidOperationException("Provider refund identity is missing"),
            ReadString(refund, "payment_intent") ?? throw new InvalidOperationException("Provider payment identity is missing"),
            refund.GetProperty("amount").GetInt64() / CurrencyFactor(currency), currency,
            ReadString(refund, "status") ?? throw new InvalidOperationException("Provider refund state is missing"), key);
    }

    private async Task<PaymentGatewayInitializeResult> InitializeStripeAsync(PaymentGatewayInitializeRequest request, CancellationToken cancellationToken)
    {
        var secretKey = _configuration["Payments:Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            EnsureSimulationAllowed();
            return SimulatedInitialize("Stripe", request);
        }

        var client = _httpClientFactory.CreateClient("Stripe");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ArgumentException("A payment idempotency key is required");
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", request.IdempotencyKey);
        var cents = ToMinorUnits(request.Amount, request.Currency);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["success_url"] = request.SuccessUrl,
            ["cancel_url"] = request.CancelUrl,
            ["client_reference_id"] = request.TransactionId,
            ["line_items[0][price_data][currency]"] = request.Currency.ToLowerInvariant(),
            ["line_items[0][price_data][product_data][name]"] = "NutriCare consultation",
            ["line_items[0][price_data][unit_amount]"] = cents.ToString(CultureInfo.InvariantCulture),
            ["line_items[0][quantity]"] = "1",
            ["metadata[transactionId]"] = request.TransactionId,
            ["payment_intent_data[metadata][transactionId]"] = request.TransactionId
        });

        var response = await client.PostAsync("https://api.stripe.com/v1/checkout/sessions", content, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Payment provider could not initialize checkout");

        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;
        return new PaymentGatewayInitializeResult(
            root.GetProperty("id").GetString() ?? request.TransactionId,
            root.TryGetProperty("url", out var url) ? url.GetString() : null,
            "pending",
            "{}",
            ReadString(root, "payment_intent"));
    }

    private async Task<PaymentGatewayRefundResult> RefundStripeAsync(string providerPaymentId, decimal amount, string currency, string? reason, CancellationToken cancellationToken, string? idempotencyKey)
    {
        var secretKey = _configuration["Payments:Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            EnsureSimulationAllowed();
            return SimulatedRefund("Stripe", providerPaymentId, amount, currency, reason);
        }

        var client = _httpClientFactory.CreateClient("Stripe");
        if (!providerPaymentId.StartsWith("pi_", StringComparison.Ordinal)) throw new InvalidOperationException("The verified payment intent is unavailable for refund");
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("A refund idempotency key is required");
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["payment_intent"] = providerPaymentId,
            ["amount"] = ToMinorUnits(amount, currency).ToString(CultureInfo.InvariantCulture),
            ["metadata[nutricare_refund_key]"] = idempotencyKey
        });

        var response = await client.PostAsync("https://api.stripe.com/v1/refunds", content, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Payment provider could not process refund");

        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;
        return new PaymentGatewayRefundResult(
            root.GetProperty("id").GetString() ?? providerPaymentId,
            ReadString(root, "status") ?? "pending",
            "{}");
    }

    private PaymentGatewayWebhookResult ParseStripeWebhook(string payload, string? signature)
    {
        var secret = _configuration["Payments:Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature)) throw new UnauthorizedAccessException("A configured signed payment callback is required");
        ValidateStripeSignature(payload, signature, secret);

        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;
        var data = root.GetProperty("data").GetProperty("object");
        var eventType = ReadString(root, "type") ?? string.Empty;
        if (eventType is "refund.created" or "refund.updated" or "refund.failed")
        {
            var refundCurrency = ReadString(data, "currency")?.ToUpperInvariant() ?? throw new InvalidOperationException("Refund currency is missing");
            var refundAmount = data.GetProperty("amount").GetInt64() / CurrencyFactor(refundCurrency);
            return new PaymentGatewayWebhookResult("", ReadString(data, "payment_intent"), ReadString(data, "status") ?? "pending", "{}",
                ReadString(root, "id") ?? throw new InvalidOperationException("Provider event identity is missing"), eventType,
                refundAmount, refundCurrency, null, null, ReadString(data, "id") ?? throw new InvalidOperationException("Refund identity is missing"));
        }
        if (eventType is not ("checkout.session.completed" or "checkout.session.async_payment_succeeded" or "checkout.session.async_payment_failed" or "checkout.session.expired")) throw new UnsupportedProviderEventException();
        var transactionId = ReadString(data, "client_reference_id");
        if (string.IsNullOrWhiteSpace(transactionId) && data.TryGetProperty("metadata", out var metadata)) transactionId = ReadString(metadata, "transactionId");
        var eventId = ReadString(root, "id");
        var sessionId = ReadString(data, "id");
        var currency = ReadString(data, "currency")?.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(currency) || !data.TryGetProperty("amount_total", out var amount) || !amount.TryGetInt64(out var minorAmount)) throw new InvalidOperationException("Payment callback is missing required fields");
        var status = eventType switch { "checkout.session.async_payment_succeeded" => "completed", "checkout.session.async_payment_failed" or "checkout.session.expired" => "failed", _ => ReadString(data, "payment_status") == "paid" ? "completed" : "pending" };
        var occurred = root.TryGetProperty("created", out var created) && created.TryGetInt64(out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : (DateTime?)null;
        return new PaymentGatewayWebhookResult(transactionId, ReadString(data, "payment_intent"), status, "{}", eventId, eventType, minorAmount / CurrencyFactor(currency), currency, occurred, sessionId);
    }

    private static PaymentGatewayInitializeResult SimulatedInitialize(string provider, PaymentGatewayInitializeRequest request)
    {
        var providerPaymentId = $"{provider.ToUpperInvariant()}-SIM-{Guid.NewGuid():N}";
        var raw = JsonSerializer.Serialize(new
        {
            provider,
            providerPaymentId,
            transactionId = request.TransactionId,
            status = "pending",
            simulated = true
        });
        return new PaymentGatewayInitializeResult(providerPaymentId, null, "pending", raw);
    }

    private static PaymentGatewayRefundResult SimulatedRefund(string provider, string providerPaymentId, decimal amount, string currency, string? reason)
    {
        var providerRefundId = $"{provider.ToUpperInvariant()}-REF-{Guid.NewGuid():N}";
        var raw = JsonSerializer.Serialize(new
        {
            provider,
            providerPaymentId,
            providerRefundId,
            amount,
            currency,
            reason,
            status = "pending",
            simulated = true
        });
        return new PaymentGatewayRefundResult(providerRefundId, "pending", raw);
    }

    public static void ValidateStripeSignature(string payload, string signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader)) throw new UnauthorizedAccessException("Invalid payment callback signature");
        var parts = signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2)
            .ToArray();
        var timestamps = parts.Where(p => p[0] == "t").Select(p => p[1]).ToArray();
        if (timestamps.Length != 1 || !long.TryParse(timestamps[0], NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (decimal)timestamp) > 300)
        {
            throw new UnauthorizedAccessException("Invalid Stripe webhook signature");
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        foreach (var candidate in parts.Where(p => p[0] == "v1"))
        {
            if (candidate[1].Length != 64) continue;
            try { if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(candidate[1]))) return; }
            catch (FormatException) { }
        }
        throw new UnauthorizedAccessException("Invalid Stripe webhook signature");
    }

    private void EnsureSimulationAllowed()
    {
        if (!_environment.IsDevelopment() || !_configuration.GetValue<bool>("Integrations:AllowSimulation")) throw new InvalidOperationException("The payment provider is not configured");
    }

    public static decimal CurrencyFactor(string currency) => currency.ToUpperInvariant() switch { "USD" or "PKR" or "GBP" or "EUR" or "AED" or "SAR" => 100m, _ => throw new InvalidOperationException("Currency is not configured for this integration") };

    private static long ToMinorUnits(decimal amount, string currency)
    {
        var units = amount * CurrencyFactor(currency);
        if (amount <= 0 || units != decimal.Truncate(units)) throw new ArgumentException("Amount has invalid currency precision");
        return checked((long)units);
    }

    private static string NormalizeProvider(string provider)
    {
        return provider.Trim() switch
        {
            var p when string.Equals(p, "Stripe", StringComparison.OrdinalIgnoreCase) => "Stripe",
            var p when string.Equals(p, "JazzCash", StringComparison.OrdinalIgnoreCase) => "JazzCash",
            var p when string.Equals(p, "EasyPaisa", StringComparison.OrdinalIgnoreCase) => "EasyPaisa",
            _ => throw new InvalidOperationException("Unsupported payment provider")
        };
    }

    private static string? ReadString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(property, out var value) &&
               value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }
}
