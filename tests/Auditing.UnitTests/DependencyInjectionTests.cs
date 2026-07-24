using ECommerce.Auditing;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Auditing.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddECommerceAuditing_ShouldRegisterRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IPublishEndpoint>());

        // Act
        services.AddECommerceAuditing();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        serviceProvider.GetService<IHttpContextAccessor>().ShouldNotBeNull();
        serviceProvider.GetService<ISaveChangesInterceptor>().ShouldBeOfType<AuditInterceptor>();
    }
}
