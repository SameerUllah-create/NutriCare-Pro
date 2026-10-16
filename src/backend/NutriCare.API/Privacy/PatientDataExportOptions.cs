namespace NutriCare.API.Privacy;

using Microsoft.Extensions.Options;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public sealed class PatientDataExportOptions
{
    public int MaxConcurrentExports { get; set; } = 2;
    public int MaxFiles { get; set; } = 200;
    public int MaxRecords { get; set; } = 20_000;
    public long MaxRecordBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxTotalFileBytes { get; set; } = 250 * 1024 * 1024;
    public long MaxArchiveBytes { get; set; } = 300 * 1024 * 1024;
    public int TimeoutSeconds { get; set; } = 120;
}

public static class PatientDataExportRegistration
{
    public static IServiceCollection AddPatientDataExport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PatientDataExportOptions>().Bind(configuration.GetSection("PrivacyExport"))
            .Validate(o => o.MaxConcurrentExports is >= 1 and <= 8 && o.MaxFiles is >= 1 and <= 1000 &&
                o.MaxRecords is >= 1 and <= 100_000 && o.MaxRecordBytes is > 0 and <= 50L * 1024 * 1024 &&
                o.MaxFileBytes is > 0 and <= 100L * 1024 * 1024 && o.MaxTotalFileBytes >= o.MaxFileBytes &&
                o.MaxTotalFileBytes <= 1024L * 1024 * 1024 && o.MaxArchiveBytes >= o.MaxTotalFileBytes &&
                o.MaxArchiveBytes <= 1200L * 1024 * 1024 && o.TimeoutSeconds is >= 1 and <= 600,
                "Privacy export limits are invalid").ValidateOnStart();
        services.AddSingleton<PatientExportResourceGate>();
        services.AddScoped(provider => new Lazy<IBlobStorageService>(() => provider.GetRequiredService<IBlobStorageService>()));
        services.AddScoped<PatientDataExportService>();
        return services;
    }
}

/// <summary>The lease lasts until the completed download stream is disposed.</summary>
public sealed class PatientExportResourceGate(IOptions<PatientDataExportOptions> options)
{
    private readonly SemaphoreSlim capacity = new(options.Value.MaxConcurrentExports, options.Value.MaxConcurrentExports);

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        if (!await capacity.WaitAsync(0, cancellationToken))
            throw new ConflictException("Export capacity is busy. Please retry shortly or request an assisted export.");
        return new Lease(capacity);
    }

    private sealed class Lease(SemaphoreSlim capacity) : IDisposable
    {
        private int disposed;
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) capacity.Release(); }
    }
}
