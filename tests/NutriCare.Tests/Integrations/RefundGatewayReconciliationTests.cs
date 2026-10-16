namespace NutriCare.Tests.Integrations;

using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using NutriCare.Infrastructure.Payments;

public sealed class RefundGatewayReconciliationTests
{
    [Fact]
    public async Task Refund_post_attaches_only_the_operation_key_and_known_refund_uses_get()
    {
        var handler = new RecordingHandler([
            "{\"id\":\"re_one\",\"status\":\"pending\"}",
            "{\"id\":\"re_one\",\"payment_intent\":\"pi_one\",\"amount\":250000,\"currency\":\"pkr\",\"status\":\"succeeded\",\"metadata\":{\"nutricare_refund_key\":\"operation-123\"}}"
        ]);
        var gateway = Gateway(handler);
        await gateway.RefundAsync("Stripe", "pi_one", 2500, "PKR", "Private consultation details", default, "operation-123");
        var state = await gateway.FindAsync("Stripe", "pi_one", "operation-123", "re_one");
        Assert.Equal(2500, state!.Amount); Assert.Equal("PKR", state.Currency);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("metadata%5Bnutricare_refund_key%5D=operation-123", handler.Requests[0].Body);
        Assert.DoesNotContain("Private", handler.Requests[0].Body);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.EndsWith("/v1/refunds/re_one", handler.Requests[1].Url);
        Assert.Null(handler.Requests[1].IdempotencyKey);
    }

    [Fact]
    public async Task Lost_response_lookup_pages_by_payment_intent_and_exact_metadata_key()
    {
        var handler = new RecordingHandler([
            "{\"has_more\":true,\"data\":[{\"id\":\"re_other\",\"payment_intent\":\"pi_one\",\"amount\":100,\"currency\":\"pkr\",\"status\":\"succeeded\",\"metadata\":{\"nutricare_refund_key\":\"other-key\"}}]}",
            "{\"has_more\":false,\"data\":[{\"id\":\"re_match\",\"payment_intent\":\"pi_one\",\"amount\":250000,\"currency\":\"pkr\",\"status\":\"pending\",\"metadata\":{\"nutricare_refund_key\":\"exact-operation\"}}]}"
        ]);
        var result = await Gateway(handler).FindAsync("Stripe", "pi_one", "exact-operation");
        Assert.Equal("re_match", result!.ProviderRefundId);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Contains("payment_intent=pi_one", handler.Requests[0].Url);
        Assert.Contains("starting_after=re_other", handler.Requests[1].Url);
    }

    [Fact]
    public async Task Incomplete_provider_listing_is_an_error_never_proof_that_no_refund_exists()
    {
        var handler = new RecordingHandler(["{\"has_more\":true,\"data\":[]}"]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Gateway(handler).FindAsync("Stripe", "pi_one", "operation"));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Missing_configuration_never_simulates_a_successful_refund_read()
    {
        var gateway = new PaymentGatewayService(new NoNetworkClients(), new ConfigurationBuilder().Build(), new FakeEnvironment());
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.FindAsync("Stripe", "pi_one", "operation"));
    }

    private static PaymentGatewayService Gateway(RecordingHandler handler) => new(new FixedClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Payments:Stripe:SecretKey"] = "test-only-key" }).Build(), new FakeEnvironment());
    private sealed class FixedClient(HttpMessageHandler handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, disposeHandler: false); }
    private sealed class RecordingHandler(IEnumerable<string> responses) : HttpMessageHandler
    {
        private readonly Queue<string> replies = new(responses);
        public List<(HttpMethod Method, string Url, string Body, string? IdempotencyKey)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.ToString(), request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(replies.Dequeue(), Encoding.UTF8, "application/json") };
        }
    }
}
