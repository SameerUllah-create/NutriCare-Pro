namespace NutriCare.API.Extensions;

using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MediatR;
using NutriCare.Application.Behaviors;
using NutriCare.Application.Handlers.Authentication;
using NutriCare.Application.Handlers.Appointments;
using NutriCare.Application.Validators;
using NutriCare.API.Hubs;
using NutriCare.API.SignalR;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Infrastructure.Authentication;
using NutriCare.Infrastructure.Email;
using NutriCare.Infrastructure.Notifications;
using NutriCare.Infrastructure.Payments;
using NutriCare.Infrastructure.Storage;
using NutriCare.Infrastructure.Video;
using NutriCare.Persistence.Contexts;
using NutriCare.Persistence.Repositories;
using FluentValidation;
using NutriCare.Application.Services;
using NutriCare.Persistence.Configuration;
using Microsoft.AspNetCore.DataProtection;
using NutriCare.API.Privacy;

/// <summary>
/// Extension methods for dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        var protection = services.AddDataProtection().SetApplicationName("NutriCare.Pro");
        if (configuration["DataProtection:KeysPath"] is string keyPath && !string.IsNullOrWhiteSpace(keyPath))
            protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        // Database
        services.AddDbContextFactory<NutriCareDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")), ServiceLifetime.Scoped);

        services
            .AddIdentity<User, Role>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultProvider;
                options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultProvider;
            })
            .AddEntityFrameworkStores<NutriCareDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(2);
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            options.AddPolicy("NutritionistOnly", policy => policy.RequireRole("Nutritionist"));
            options.AddPolicy("PatientOnly", policy => policy.RequireRole("Patient"));
            options.AddPolicy("ClinicalStaff", policy => policy.RequireRole("Admin", "Nutritionist"));
        });

        // Repository and Unit of Work
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentReservationLock, AppointmentReservationLock>();
        services.AddScoped<IClinicalAccessService, ClinicalAccessService>();
        services.AddScoped<IClinicalSchedulingService, ClinicalSchedulingService>();
        services.AddScoped<ClinicalAppointmentWorkflow>();
        services.AddScoped<ClinicalPlanWorkflow>();
        services.AddSingleton<IClinicalPdfRenderer, NutriCare.Infrastructure.ClinicalPdfRenderer>();
        services.AddScoped<ConfigurationStore>();
        services.AddScoped<PackageCreditStore>();
        services.AddScoped<IPackageCreditService>(p => p.GetRequiredService<PackageCreditStore>());
        services.AddPatientDataExport(configuration);
        services.AddScoped<ICancellationRefundOutbox, CancellationRefundOutbox>();
        services.AddScoped<PaymentRefundWorkflow>();
        services.AddScoped<CancellationRefundProcessor>();
        services.AddScoped<IPaymentRefundReconciliation>(p => (IPaymentRefundReconciliation)p.GetRequiredService<IPaymentGatewayService>());
        services.AddHostedService<NutriCare.API.BackgroundServices.CancellationRefundWorker>();
        services.AddHostedService<NutriCare.API.BackgroundServices.NotificationDeliveryWorker>();

        // Authentication and Authorization
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<INotificationService>(p => p.GetRequiredService<NotificationService>());
        services.AddScoped<INotificationOutbox>(p => p.GetRequiredService<NotificationService>());
        services.AddScoped<IBlobStorageService>(p => configuration["Storage:Provider"] == "Local"
            ? ActivatorUtilities.CreateInstance<DevelopmentFileStorageService>(p)
            : ActivatorUtilities.CreateInstance<AzureBlobStorageService>(p));
        services.AddScoped<IPaymentGatewayService, PaymentGatewayService>();
        services.AddScoped<VideoMeetingService>();
        services.AddScoped<IVideoMeetingService>(p => p.GetRequiredService<VideoMeetingService>());
        services.AddScoped<IVideoMeetingLifecycleService>(p => p.GetRequiredService<VideoMeetingService>());
        services.AddHostedService<NutriCare.API.BackgroundServices.VideoMeetingLifecycleWorker>();
        services.AddScoped<IClinicalConsentService, ClinicalConsentService>();
        services.AddHttpClient();
        services.AddSingleton<IChatPresenceTracker, ChatPresenceTracker>();
        services.AddSingleton<IUserIdProvider, UserIdProvider>();
        services.AddMemoryCache();
        services.AddOutputCache(options =>
        {
            options.AddPolicy("PublicMarketing", policy => policy.Expire(TimeSpan.FromMinutes(10)));
            options.AddPolicy("ShortApi", policy => policy.Expire(TimeSpan.FromSeconds(30)));
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("Authentication", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, QueueLimit = 0, Window = TimeSpan.FromMinutes(1) }));
            options.AddPolicy("AuthenticatedApi", httpContext =>
            {
                var userId = httpContext.User.FindFirst("uid")?.Value;
                var key = string.IsNullOrWhiteSpace(userId)
                    ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous"
                    : $"user:{userId}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    });
            });
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });

        // MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(
                typeof(LoginCommandHandler).Assembly,
                typeof(CreateAppointmentCommandHandler).Assembly
            );
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // Validation
        services.AddValidatorsFromAssemblyContaining<LoginCommandValidator>();

        // HTTP Context
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>
    /// Add JWT authentication
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        if (Encoding.UTF8.GetByteCount(secretKey) < 32 ||
            !string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase) && secretKey.Contains("your-super-secret",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Provide a strong JWT signing key through secure environment configuration");
        var issuer = jwtSettings["Issuer"] ?? "NutriCare";
        var audience = jwtSettings["Audience"] ?? "NutriCareUsers";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = "role",
                NameClaimType = "email"
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // Browser transports may only supply a short-lived access token to these exact hubs.
                    if (context.HttpContext.Request.Path.StartsWithSegments("/hubs/chat") || context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                        context.Token = context.Request.Query["access_token"].FirstOrDefault();
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal!;
                    if (!long.TryParse(principal.FindFirst("uid")?.Value, out var userId)) { context.Fail("Invalid account"); return; }
                    var db = context.HttpContext.RequestServices.GetRequiredService<NutriCareDbContext>();
                    var stamp = principal.FindFirst("sv")?.Value;
                    var sessionId = principal.FindFirst("sid")?.Value;
                    var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null);
                    if (user == null || !user.IsActive || !user.EmailConfirmed || string.IsNullOrWhiteSpace(stamp) || user.SecurityStamp != stamp || string.IsNullOrWhiteSpace(sessionId)
                        || !await db.AuthSessions.AsNoTracking().AnyAsync(s => s.UserId == userId && s.SessionId == sessionId && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow && s.SecurityStamp == stamp))
                        context.Fail("Session expired or revoked");
                },
                OnAuthenticationFailed = context =>
                {
                    context.NoResult();
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    var result = System.Text.Json.JsonSerializer.Serialize(new { error = "Authorization required" });
                    return context.Response.WriteAsync(result);
                }
            };
        });

        return services;
    }

    /// <summary>
    /// Add Swagger/OpenAPI documentation
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "NutriCare Pro API",
                Version = "v1.0",
                Description = "ASP.NET Core 10 Web API for Nutrition Consultation Platform",
                Contact = new Microsoft.OpenApi.Models.OpenApiContact
                {
                    Name = "NutriCare Support",
                    Email = "support@nutricare.com"
                }
            });

            // Add JWT security definition
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Description = "Please enter into field the word 'Bearer' following by space and JWT",
                Name = "Authorization",
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
            });

            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] { }
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Add CORS policy
    /// </summary>
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                var origins = configuration.GetSection("AllowedOrigins").Get<string[]>()
                    ?? ["http://localhost:3000", "http://localhost:3001"];

                policy.WithOrigins(origins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
