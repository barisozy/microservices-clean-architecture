using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ECommerce.Auditing;

public static class DependencyInjection
{
    public static IServiceCollection AddECommerceAuditing(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        services.AddScoped<ISaveChangesInterceptor, AuditInterceptor>();
        return services;
    }
}
