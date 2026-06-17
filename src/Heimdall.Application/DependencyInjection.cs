using System.Reflection;
using FluentValidation;
using Heimdall.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR handlers from this assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Pipeline behaviors execute in registration order (outermost first).
            // 1. PerformanceLogBehavior — captures total pipeline execution time
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceLogBehavior<,>));

            // 2. ValidationBehavior — rejects invalid requests early
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            // 3. AuditBehavior — captures before/after state changes
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

            // 4. CacheInvalidationBehavior — invalidates cache after successful mutation
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationBehavior<,>));
        });

        // Register all FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
