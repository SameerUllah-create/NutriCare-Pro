namespace NutriCare.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using NutriCare.Domain.Interfaces;

/// <summary>Local, isolated browser rendering. Clinical HTML is never sent to an external conversion service.</summary>
public sealed class ClinicalPdfRenderer(IConfiguration configuration) : IClinicalPdfRenderer, IAsyncDisposable
{
    private readonly SemaphoreSlim _initialization = new(1, 1);
    private readonly SemaphoreSlim _capacity = new(2, 2);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html) || html.Length > 2_000_000) throw new ArgumentException("The printable plan is empty or too large");
        await _capacity.WaitAsync(cancellationToken);
        try
        {
            var browser = await BrowserAsync(cancellationToken);
            await using var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false, Offline = true, Locale = "en-PK" });
            await context.RouteAsync("**/*", route => route.AbortAsync());
            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(15000);
            await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.Load, Timeout = 15000 });
            cancellationToken.ThrowIfCancellationRequested();
            return await page.PdfAsync(new() { Format = "A4", Tagged = true, Outline = true, PrintBackground = true,
                PreferCSSPageSize = true, Margin = new() { Top = "16mm", Right = "14mm", Bottom = "16mm", Left = "14mm" } });
        }
        finally { _capacity.Release(); }
    }

    private async Task<IBrowser> BrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsConnected: true }) return _browser;
        await _initialization.WaitAsync(cancellationToken);
        try
        {
            if (_browser is { IsConnected: true }) return _browser;
            _playwright ??= await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true,
                ExecutablePath = configuration["ClinicalPdf:ExecutablePath"], Timeout = 30000 });
            return _browser;
        }
        finally { _initialization.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        _capacity.Dispose();
        _initialization.Dispose();
    }
}
