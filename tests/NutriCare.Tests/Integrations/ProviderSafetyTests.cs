namespace NutriCare.Tests.Integrations;

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NutriCare.Infrastructure.Email;
using NutriCare.Infrastructure.Video;

public class ProviderSafetyTests
{
    [Fact]
    public async Task DevelopmentMailboxSavesResetLinkWithoutPuttingItInLogs()
    {
        var root = Path.Combine(Path.GetTempPath(), "NutriCareIntegrationTests", Guid.NewGuid().ToString("N"));
        var logger = new CapturingLogger<EmailService>();
        var service = new EmailService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Integrations:DevelopmentMailDirectory"] = root }).Build(), logger, new FakeEnvironment { EnvironmentName = "Development" });
        const string link = "https://example.test/reset?token=secret-test-reset-token";
        try
        {
            Assert.True(await service.SendPasswordResetEmailAsync("patient@example.test", link));
            var file = Assert.Single(Directory.GetFiles(root, "*.eml"));
            Assert.Contains(link, await File.ReadAllTextAsync(file));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("secret-test-reset-token", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("patient@example.test", StringComparison.Ordinal));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ProductionCannotUseDevelopmentMailboxAsSuccessfulEmailDelivery()
    {
        var service = new EmailService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Integrations:DevelopmentMailDirectory"] = "should-not-write" }).Build(), new CapturingLogger<EmailService>(), new FakeEnvironment());
        Assert.False(await service.SendVerificationEmailAsync("patient@example.test", "https://example.test/verify?token=test"));
    }

    [Fact]
    public async Task ZoomCancellationUsesDocumentedDeleteAndTreatsMissingMeetingAsIdempotent()
    {
        var transport = new RecordingHttpHandler(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"code\":3001}") });
        var service = new VideoMeetingService(new FixedHttpClients(transport), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["VideoConsultations:Zoom:AccessToken"] = "test-only-token" }).Build(), new FakeEnvironment());
        Assert.Equal("Revoked", await service.CancelMeetingAsync("Zoom", "123456789"));
        Assert.Equal(HttpMethod.Delete, transport.Method); Assert.Equal("https://api.zoom.us/v2/meetings/123456789", transport.Url);
    }

    [Fact]
    public async Task GoogleCancellationDoesNotFalselyClaimSavedUrlWasRevoked()
    {
        var service = new VideoMeetingService(new NoNetworkClients(), new ConfigurationBuilder().Build(), new FakeEnvironment());
        Assert.Equal("ManualActionRequired", await service.CancelMeetingAsync("GoogleMeet", "spaces/test"));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class FixedHttpClients(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHttpHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Url { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Method = request.Method; Url = request.RequestUri?.ToString(); return Task.FromResult(response); }
    }
}
