using Microsoft.Extensions.DependencyInjection;
using Inventory.Application;
using Xunit;

namespace Inventory.UnitTests;

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
