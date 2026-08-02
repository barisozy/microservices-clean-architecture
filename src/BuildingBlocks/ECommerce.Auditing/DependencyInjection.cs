using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Auditing;

public static class DependencyInjection
{
    public static IServiceCollection AddECommerceAuditing(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor>(provider =>
            provider.GetRequiredService<AuditableEntityInterceptor>());
        return services;
    }
}
