using Microsoft.Extensions.DependencyInjection;
using Payment.Application;
using Xunit;

namespace Payment.UnitTests;

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
