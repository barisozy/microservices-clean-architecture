using PactNet;
using PactNet.Infrastructure.Outputters;
using Xunit;

namespace ECommerce.ContractTests;

public class XUnitOutput : IOutput
{
    public void WriteLine(string line)
    {
        Console.WriteLine(line);
    }
}

public class ConsumerContractTest
{
    private readonly IPactBuilderV3 _pactBuilder;

    public ConsumerContractTest()
    {
        var config = new PactConfig
        {
            PactDir = "../../../pacts/",
            Outputters = new[] { new XUnitOutput() },
            DefaultJsonSettings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
            }
        };

        var pact = Pact.V3("GatewayBFF", "OrderingAPI", config);
        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact]
    public async Task GetOrders_WhenCalled_ReturnsExpectedResponse()
    {
        // Arrange - Setup the expected interactions (The Contract)
        _pactBuilder
            .UponReceiving("A request to get orders for a user")
            .Given("orders exist for the user")
            .WithRequest(HttpMethod.Get, "/api/orders")
            .WithHeader("Authorization", "Bearer [Token]")
            .WillRespond()
            .WithStatus(System.Net.HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                orders = new[]
                {
                    new { id = "123", status = "Shipped", total = 100.50 }
                }
            });

        // Act & Assert - Run the mock provider and test the consumer logic
        await _pactBuilder.VerifyAsync(async ctx =>
        {
            // Simulate the API Gateway / Consumer making the HTTP call to the Mock Provider
            var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("Authorization", "Bearer [Token]");
            
            var response = await client.GetAsync("/api/orders");
            
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("123", content);
            Assert.Contains("Shipped", content);
        });
    }
}
