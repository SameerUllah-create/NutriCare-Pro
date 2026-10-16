using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NutriCare.API.Extensions;
using NutriCare.API.Middleware;
using Microsoft.EntityFrameworkCore;
using NutriCare.Persistence.Configuration;
using NutriCare.Persistence.Contexts;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "NutriCare.API")
    .WriteTo.Console()
    .WriteTo.File("logs/nutricare-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services
    .AddApplicationServices(builder.Configuration)
    .AddJwtAuthentication(builder.Configuration)
    .AddSwaggerDocumentation()
    .AddCorsPolicy(builder.Configuration)
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Add SignalR for real-time communication
builder.Services.AddSignalR();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NutriCare.Persistence.Contexts.NutriCareDbContext>("sql-server")
    .AddCheck("self", () => HealthCheckResult.Healthy("API is running"));

var app = builder.Build();
var trustedProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
if (trustedProxies.Length > 0)
{
    var forwarded = new ForwardedHeadersOptions
    { ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto, ForwardLimit = 1 };
    forwarded.KnownIPNetworks.Clear(); forwarded.KnownProxies.Clear();
    foreach (var proxy in trustedProxies) forwarded.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    app.UseForwardedHeaders(forwarded);
}

// Configure the HTTP request pipeline
app.UseGlobalExceptionHandling();
app.UseSecurityHeaders();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NutriCare API v1");
        c.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseOutputCache();
app.UseAuditLogging();
app.UseAuthorization();

// Map health checks
app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                duration = entry.Value.Duration.TotalMilliseconds
            })
        };

        await context.Response.WriteAsJsonAsync(response);
    }
});
app.MapGet("/api/health/live", () => Results.Ok(new { status = "Healthy" }));

// Map controllers
app.MapControllers().RequireRateLimiting("AuthenticatedApi");

// Map SignalR hubs
app.MapHub<NutriCare.API.Hubs.ChatHub>("/hubs/chat");
app.MapHub<NutriCare.API.Hubs.NotificationHub>("/hubs/notifications");

// Run migrations
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<NutriCare.Persistence.Contexts.NutriCareDbContext>();
        if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
        {
            await dbContext.Database.MigrateAsync();
            Log.Information("Tracked database migrations applied");
        }
        if (builder.Configuration.GetValue<bool>("Database:SeedProposedConfiguration"))
            await ProposedConfigurationSeeder.SeedAsync(dbContext);
        await NutriCare.API.Setup.InitialAdministrator.ProvisionAsync(scope.ServiceProvider, builder.Configuration);
    }
}

catch (Exception ex)
{
    Log.Fatal(ex, "An error occurred while initializing the database");
    throw;
}

try
{
    Log.Information("Starting NutriCare API...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
