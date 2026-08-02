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

        var pact = Pact.V3("GatewayBFF", "OrderAPI", config);
        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact]
    public async Task GetOrders_WhenCalled_ReturnsExpectedResponse()
    {
        _pactBuilder
            .UponReceiving("A request to get orders for a user")
            .Given("orders exist for the user")
            .WithRequest(HttpMethod.Get, "/api/v1/orders")
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

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("Authorization", "Bearer [Token]");
            
            var response = await client.GetAsync("/api/v1/orders");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("123", content);
        });
    }

    [Fact]
    public async Task GetCatalogProducts_WhenCalled_ReturnsExpectedProducts()
    {
        var pact = Pact.V3("GatewayBFF", "CatalogAPI", new PactConfig { PactDir = "../../../pacts/", Outputters = new[] { new XUnitOutput() } });
        var builder = pact.WithHttpInteractions();

        builder
            .UponReceiving("A request to get catalog products")
            .WithRequest(HttpMethod.Get, "/api/v1/catalog/products")
            .WillRespond()
            .WithStatus(System.Net.HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new[]
            {
                new { sku = "SKU-TEST-001", name = "Test Product", price = 49.99 }
            });

        await builder.VerifyAsync(async ctx =>
        {
            var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            var response = await client.GetAsync("/api/v1/catalog/products");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("SKU-TEST-001", content);
        });
    }

    [Fact]
    public async Task SearchProducts_WhenCalled_ReturnsSearchResults()
    {
        var pact = Pact.V3("GatewayBFF", "SearchAPI", new PactConfig { PactDir = "../../../pacts/", Outputters = new[] { new XUnitOutput() } });
        var builder = pact.WithHttpInteractions();

        builder
            .UponReceiving("A request to search products")
            .WithRequest(HttpMethod.Get, "/api/v1/search")
            .WithQuery("q", "test")
            .WillRespond()
            .WithStatus(System.Net.HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new[]
            {
                new { sku = "SKU-TEST-001", name = "Test Product" }
            });

        await builder.VerifyAsync(async ctx =>
        {
            var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            var response = await client.GetAsync("/api/v1/search?q=test");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("SKU-TEST-001", content);
        });
    }
}
