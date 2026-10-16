namespace NutriCare.Tests.Integrations;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using NutriCare.Domain.Interfaces;
using NutriCare.Infrastructure.Payments;

public class PaymentSignatureTests
{
    private const string Secret = "whsec_test_only_not_a_live_credential";
    private static readonly string Payload = "{\"id\":\"evt_test\",\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_test\",\"client_reference_id\":\"transaction\",\"payment_intent\":\"pi_test\",\"payment_status\":\"paid\",\"currency\":\"pkr\",\"amount_total\":500000}}}";
    private static PaymentGatewayService Gateway(string? secret = Secret, string environment = "Production", bool simulate = false) =>
        new(new NoNetworkClients(), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Payments:Stripe:WebhookSecret"] = secret, ["Integrations:AllowSimulation"] = simulate.ToString() }).Build(), new FakeEnvironment { EnvironmentName = environment });
    private static string Signature(long timestamp, string payload = "") => $"t={timestamp},v1={Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{timestamp}.{(payload == "" ? Payload : payload)}"))).ToLowerInvariant()}";

    [Fact]
    public async Task MissingSignatureIsRejectedEvenWithConfiguredSecret() => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Gateway().ParseWebhookAsync("Stripe", Payload, null));

    [Fact]
    public async Task MissingSecretIsRejectedEvenInExplicitDevelopmentSimulation() => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Gateway(null, "Development", true).ParseWebhookAsync("Stripe", Payload, Signature(DateTimeOffset.UtcNow.ToUnixTimeSeconds())));

    [Fact]
    public async Task StaleValidSignatureIsRejected() => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Gateway().ParseWebhookAsync("Stripe", Payload, Signature(DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeSeconds())));

    [Fact]
    public async Task FutureValidSignatureIsRejected() => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Gateway().ParseWebhookAsync("Stripe", Payload, Signature(DateTimeOffset.UtcNow.AddMinutes(6).ToUnixTimeSeconds())));

    [Fact]
    public async Task TamperedBodyIsRejected() => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Gateway().ParseWebhookAsync("Stripe", Payload.Replace("500000", "100"), Signature(DateTimeOffset.UtcNow.ToUnixTimeSeconds())));

    [Fact]
    public async Task RotatedMultipleSignaturesAreAcceptedAndIntentIsSeparateFromSession()
    {
        var header = Signature(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) + ",v1=" + new string('0', 64);
        var result = await Gateway().ParseWebhookAsync("Stripe", Payload, header);
        Assert.Equal("pi_test", result.ProviderPaymentId); Assert.Equal("cs_test", result.ProviderSessionId);
        Assert.Equal("evt_test", result.EventId); Assert.Equal(5000m, result.Amount); Assert.Equal("PKR", result.Currency);
    }

    [Fact]
    public async Task ProductionDoesNotSimulateWithoutProviderKey() => await Assert.ThrowsAsync<InvalidOperationException>(() => Gateway().InitializePaymentAsync("Stripe", new PaymentGatewayInitializeRequest(100, "PKR", "Consultation", "transaction", "https://example.test/success", "https://example.test/cancel")));

    [Fact]
    public async Task UnapprovedWalletContractIsDisabled() => await Assert.ThrowsAsync<InvalidOperationException>(() => Gateway().ParseWebhookAsync("JazzCash", "{}", "signature"));
}
