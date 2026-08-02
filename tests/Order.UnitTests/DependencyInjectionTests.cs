using Microsoft.Extensions.DependencyInjection;
using Order.Application;
using Xunit;

namespace Order.UnitTests;

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

