using ECommerce.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Audit.UnitTests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddECommerceAuditing_RegistersOneScopedInterceptorInstance()
    {
        var services = new ServiceCollection();
        services.AddECommerceAuditing();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var concrete = scope.ServiceProvider.GetRequiredService<AuditableEntityInterceptor>();

        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<ISaveChangesInterceptor>().ShouldBeSameAs(concrete);
    }
}
