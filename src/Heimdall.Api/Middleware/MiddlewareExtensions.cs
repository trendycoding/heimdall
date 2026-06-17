namespace Heimdall.Api.Middleware;

public static class MiddlewareExtensions
{
    /// <summary>
    /// Registers the Heimdall middleware pipeline in the correct order:
    /// 1. ExceptionHandlingMiddleware — Global exception to envelope conversion
    /// 2. CorrelationIdMiddleware — Generate/propagate unique UUID v4 correlation ID
    /// 3. ApiCallLogMiddleware — Capture request start, write log on response
    /// 4. TokenValidationMiddleware — Validate bearer token / API key
    /// 5. TenantResolutionMiddleware — Resolve tenant from route > token > client > key
    /// 6. AdminAuthorizationMiddleware — Verify required admin scopes
    /// </summary>
    public static IApplicationBuilder UseHeimdallMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ApiCallLogMiddleware>();
        app.UseMiddleware<TokenValidationMiddleware>();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseMiddleware<AdminAuthorizationMiddleware>();

        return app;
    }
}
