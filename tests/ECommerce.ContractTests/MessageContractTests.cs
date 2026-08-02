using ECommerce.Contracts.Events.v1;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PactNet;
using Xunit;

namespace ECommerce.ContractTests;

public sealed class MessageContractTests
{
    [Fact]
    public void Notification_ConsumesOrderShipped()
    {
        var expected = new OrderShipped(
            Guid.Parse("01910000-0000-7000-8000-000000000001"),
            "TRACK-12345678",
            DateTimeOffset.Parse("2026-08-02T12:00:00Z"));

        VerifyMessage("NotificationAPI", "OrderAPI", "an order shipped event", expected,
            actual =>
            {
                Assert.Equal(expected.OrderId, actual.OrderId);
                Assert.Equal(expected.TrackingId, actual.TrackingId);
            });
    }

    [Fact]
    public void Notification_ConsumesPaymentFailed()
    {
        var expected = new PaymentFailed(
            Guid.Parse("01910000-0000-7000-8000-000000000002"),
            "01910000-0000-7000-8000-000000000003",
            "Card declined",
            DateTimeOffset.Parse("2026-08-02T12:01:00Z"));

        VerifyMessage("NotificationAPI", "PaymentAPI", "a payment failed event", expected,
            actual =>
            {
                Assert.Equal(expected.OrderId, actual.OrderId);
                Assert.Equal(expected.Reason, actual.Reason);
            });
    }

    [Fact]
    public void Search_ConsumesProductUpserted()
    {
        var expected = new ProductUpserted("SKU-TEST-001", "Test Product", 49.99m);

        VerifyMessage("SearchAPI", "CatalogAPI", "a product upserted event", expected,
            actual =>
            {
                Assert.Equal(expected.Sku, actual.Sku);
                Assert.Equal(expected.Price, actual.Price);
            });
    }

    [Fact]
    public void Customer_ConsumesUserRegistered()
    {
        var expected = new UserRegistered(
            Guid.Parse("01910000-0000-7000-8000-000000000004"),
            "customer@example.test");

        VerifyMessage("CustomerAPI", "IAMAPI", "a user registered event", expected,
            actual =>
            {
                Assert.Equal(expected.KeycloakSubject, actual.KeycloakSubject);
                Assert.Equal(expected.Email, actual.Email);
            });
    }

    private static void VerifyMessage<T>(
        string consumer,
        string provider,
        string description,
        T expected,
        Action<T> assert)
        where T : class
    {
        var config = new PactConfig
        {
            PactDir = "../../../pacts/",
            Outputters = [new XUnitOutput()],
            DefaultJsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }
        };

        Pact.V3(consumer, provider, config)
            .WithMessageInteractions()
            .ExpectsToReceive(description)
            .WithMetadata("contentType", "application/json")
            .WithJsonContent(expected)
            .Verify<T>(assert);
    }
}
