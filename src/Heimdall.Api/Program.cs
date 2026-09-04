using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Heimdall.Api.Configuration;
using Heimdall.Api.Controllers;
using Heimdall.Api.Filters;
using Heimdall.Api.HealthChecks;
using Heimdall.Api.Middleware;
using Heimdall.Application;
using Heimdall.Domain.Enums;
using Heimdall.Infrastructure;
using Heimdall.Infrastructure.Cloud;
using Heimdall.Infrastructure.Logging;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Cloud provider selection (Azure | Aws | None). Defaults to Azure.
// Azure-specific configuration sources are only wired when running on Azure.
// ---------------------------------------------------------------------------
var cloudProvider = CloudServiceRegistration.ResolveProvider(builder.Configuration);
var isAzure = cloudProvider == CloudProvider.Azure;

var appConfigConnectionString = builder.Configuration.GetConnectionString("AppConfiguration");
var useAzureAppConfig = isAzure && !string.IsNullOrWhiteSpace(appConfigConnectionString);

if (useAzureAppConfig)
{
    // Configuration: Azure App Configuration with sentinel-based refresh
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(appConfigConnectionString)
            .ConfigureRefresh(refresh =>
            {
                refresh.Register("Sentinel", refreshAll: true)
                    .SetCacheExpiration(TimeSpan.FromSeconds(30));
            });
    });

    builder.Services.AddAzureAppConfiguration();
}

// Configuration: Azure Key Vault as a configuration source (Azure only).
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (isAzure && !string.IsNullOrWhiteSpace(keyVaultUri))
{
    var secretClientOptions = new Azure.Security.KeyVault.Secrets.SecretClientOptions
    {
        Retry =
        {
            MaxRetries = 3,
            Delay = TimeSpan.FromMilliseconds(800),
            MaxDelay = TimeSpan.FromSeconds(10),
            Mode = Azure.Core.RetryMode.Exponential
        }
    };

    var credential = new DefaultAzureCredential();
    var secretClient = new Azure.Security.KeyVault.Secrets.SecretClient(
        new Uri(keyVaultUri), credential, secretClientOptions);

    builder.Configuration.AddAzureKeyVault(secretClient, new Azure.Extensions.AspNetCore.Configuration.Secrets.KeyVaultSecretManager());
}

// ---------------------------------------------------------------------------
// TLS 1.2+ enforcement
// ---------------------------------------------------------------------------
System.Net.ServicePointManager.SecurityProtocol =
    System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;

// ---------------------------------------------------------------------------
// Service Registration
// ---------------------------------------------------------------------------

// HttpContextAccessor (required by AuditBehavior for CorrelationId, ApiCallLogId, SourceIp, UserAgent)
builder.Services.AddHttpContextAccessor();

// Application layer (MediatR, FluentValidation, pipeline behaviors)
builder.Services.AddApplication();

// Infrastructure layer (EF Core, repositories, services, caching)
builder.Services.AddInfrastructure(builder.Configuration);

// Seed data (development environment only)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDevelopmentSeedData();
}

// Controllers with global filters
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationExceptionFilter>();
    options.Filters.Add<EnvelopeResultFilter>();
});

// Rate limiting
builder.Services.AddRateLimitingConfiguration();

// HTTP client factory (used by TokenValidationService for JWKS fetching)
builder.Services.AddHttpClient();

// Swagger/OpenAPI (local development only)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Heimdall Access API",
        Version = "v1",
        Description = "Multi-tenant security and access management platform."
    });
});

// Cloud-agnostic telemetry (custom metrics via System.Diagnostics.Metrics)
builder.Services.AddHeimdallTelemetry();

// Azure Application Insights (only when running on Azure with a connection string)
var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (isAzure && !string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });

    // Azure-specific telemetry enrichment (CorrelationId initializer, sensitive data redaction)
    builder.Services.AddAzureApplicationInsightsEnrichment();
}

// Structured logging (JSON console with timestamp, severity, CorrelationId, source)
builder.Logging.AddHeimdallStructuredLogging();

// ---------------------------------------------------------------------------
// Health Checks: Database, Redis (if enabled), Key Vault
// ---------------------------------------------------------------------------
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddDbContextCheck<HeimdallDbContext>(
        name: "database",
        tags: new[] { "db", "sql" });

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    healthChecksBuilder.AddCheck<RedisHealthCheck>(
        name: "redis",
        tags: new[] { "cache", "redis" },
        timeout: HealthCheckConfiguration.Timeout);
}

// Secret store health check — cloud-agnostic via ISecretProvider
healthChecksBuilder.AddCheck<SecretStoreHealthCheck>(
    name: "secret-store",
    tags: new[] { "secrets" },
    timeout: HealthCheckConfiguration.Timeout);

// ---------------------------------------------------------------------------
// Build App
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// HTTP Pipeline
// ---------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    // Apply pending EF Core migrations automatically on startup
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HeimdallDbContext>();
    await db.Database.MigrateAsync();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Heimdall Access API v1");
    });
}

app.UseHttpsRedirection();

// Azure App Configuration refresh middleware — checks sentinel key on each request
// and triggers configuration reload when sentinel value changes (30s cache interval)
if (useAzureAppConfig)
{
    app.UseAzureAppConfiguration();
}

// Rate limiting middleware
app.UseRateLimiter();

// Heimdall middleware pipeline (order matters):
// 1. ExceptionHandling, 2. CorrelationId, 3. ApiCallLog,
// 4. TokenValidation, 5. TenantResolution, 6. AdminAuthorization
app.UseHeimdallMiddleware();

app.UseAuthorization();

// Health check endpoint (bypasses auth middleware, responds within 5s)
app.MapHeimdallHealthChecks();

app.MapControllers();

await app.RunAsync();

// Make the implicit Program class public so test projects can access it
public partial class Program { }
