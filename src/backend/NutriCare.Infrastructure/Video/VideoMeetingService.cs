namespace NutriCare.Infrastructure.Video;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NutriCare.Domain.Interfaces;

public class VideoMeetingService : IVideoMeetingService, IVideoMeetingLifecycleService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public VideoMeetingService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IHostEnvironment environment)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _environment = environment;
    }

    public Task<VideoMeetingCreateResult> CreateMeetingAsync(
        string provider,
        VideoMeetingCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        return NormalizeProvider(provider) switch
        {
            "GoogleMeet" => CreateGoogleMeetAsync(request, cancellationToken),
            "Zoom" => CreateZoomMeetingAsync(request, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported video meeting provider")
        };
    }

    public async Task<string> CancelMeetingAsync(string provider, string providerMeetingId, CancellationToken cancellationToken = default)
    {
        provider = NormalizeProvider(provider);
        if (providerMeetingId.StartsWith("ZOOM-", StringComparison.Ordinal) || providerMeetingId.StartsWith("GOOGLEMEET-", StringComparison.Ordinal))
        {
            EnsureSimulationAllowed();
            return "Revoked";
        }
        if (provider == "GoogleMeet") return "ManualActionRequired";
        var token = _configuration["VideoConsultations:Zoom:AccessToken"];
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Video provider cancellation credentials are unavailable");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(providerMeetingId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _httpClientFactory.CreateClient("Zoom").SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return "Revoked";
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (json.RootElement.TryGetProperty("code", out var code) && code.TryGetInt32(out var number) && number == 3001) return "Revoked";
        }
        throw new InvalidOperationException("Video provider did not confirm meeting cancellation");
    }

    private async Task<VideoMeetingCreateResult> CreateGoogleMeetAsync(VideoMeetingCreateRequest request, CancellationToken cancellationToken)
    {
        var accessToken = _configuration["VideoConsultations:GoogleMeet:AccessToken"];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            EnsureSimulationAllowed();
            return SimulatedMeeting("GoogleMeet", request);
        }

        var client = _httpClientFactory.CreateClient("GoogleMeet");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var body = new
        {
            config = new { accessType = "RESTRICTED" }
        };

        var response = await client.PostAsync(
            "https://meet.googleapis.com/v2/spaces",
            JsonContent(body),
            cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Video provider could not create the meeting");

        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;
        var meetingUrl = ReadString(root, "meetingUri") ?? ReadString(root, "meetingUrl") ?? string.Empty;
        return new VideoMeetingCreateResult(
            "GoogleMeet",
            ReadString(root, "name") ?? Guid.NewGuid().ToString("N"),
            meetingUrl,
            null,
            "{}");
    }

    private async Task<VideoMeetingCreateResult> CreateZoomMeetingAsync(VideoMeetingCreateRequest request, CancellationToken cancellationToken)
    {
        var accessToken = _configuration["VideoConsultations:Zoom:AccessToken"];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            EnsureSimulationAllowed();
            return SimulatedMeeting("Zoom", request);
        }

        var client = _httpClientFactory.CreateClient("Zoom");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var body = new
        {
            topic = request.Topic,
            type = 2,
            start_time = request.StartTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            duration = Math.Max(1, (int)Math.Ceiling((request.EndTime - request.StartTime).TotalMinutes)),
            timezone = request.TimeZone,
            agenda = "Secure nutrition consultation",
            settings = new { waiting_room = true, join_before_host = false, approval_type = 2 }
        };

        var response = await client.PostAsync("https://api.zoom.us/v2/users/me/meetings", JsonContent(body), cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Video provider could not create the meeting");

        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;
        return new VideoMeetingCreateResult(
            "Zoom",
            ReadString(root, "id") ?? Guid.NewGuid().ToString("N"),
            ReadString(root, "join_url") ?? string.Empty,
            ReadString(root, "start_url"),
            "{}");
    }

    private static VideoMeetingCreateResult SimulatedMeeting(string provider, VideoMeetingCreateRequest request)
    {
        var id = $"{provider.ToUpperInvariant()}-{Guid.NewGuid():N}";
        var urlProvider = provider == "Zoom" ? "zoom" : "meet.google";
        var meetingUrl = $"https://{urlProvider}.example.com/{id}";
        var raw = JsonSerializer.Serialize(new
        {
            provider,
            id,
            meetingUrl,
            request.Topic,
            request.StartTime,
            request.EndTime,
            simulated = true
        });

        return new VideoMeetingCreateResult(provider, id, meetingUrl, meetingUrl, raw);
    }

    private void EnsureSimulationAllowed()
    {
        if (!_environment.IsDevelopment() || !_configuration.GetValue<bool>("Integrations:AllowSimulation"))
            throw new InvalidOperationException("The video provider is not configured");
    }

    private static string NormalizeProvider(string provider)
    {
        return provider.Trim() switch
        {
            var p when string.Equals(p, "GoogleMeet", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(p, "Google Meet", StringComparison.OrdinalIgnoreCase) => "GoogleMeet",
            var p when string.Equals(p, "Zoom", StringComparison.OrdinalIgnoreCase) => "Zoom",
            _ => throw new InvalidOperationException("Unsupported video meeting provider")
        };
    }

    private static StringContent JsonContent(object value)
    {
        return new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
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
