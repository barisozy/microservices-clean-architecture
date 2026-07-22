using Microsoft.Extensions.DependencyInjection;
using Payments.Application;
using Xunit;

namespace Payments.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplicationServices_Should_Register_Services()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        
        Assert.NotEmpty(services);
    }
}
