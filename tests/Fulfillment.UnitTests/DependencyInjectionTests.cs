using Microsoft.Extensions.DependencyInjection;
using Fulfillment.Application;
using Xunit;

namespace Fulfillment.UnitTests;

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
