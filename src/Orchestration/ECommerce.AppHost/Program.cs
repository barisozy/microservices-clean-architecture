Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure Containers
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("18.4")
    .WithPgAdmin();

var orderingDb = postgres.AddDatabase("OrderingDb", "ordering_db");
var inventoryDb = postgres.AddDatabase("InventoryDb", "inventory_db");
var paymentsDb = postgres.AddDatabase("PaymentsDb", "payments_db");
var fulfillmentDb = postgres.AddDatabase("FulfillmentDb", "fulfillment_db");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

// Valkey 9.1 (BSD-3-Clause Redis-protocol store)
var redis = builder.AddRedis("redis")
    .WithImage("valkey/valkey", "9.1");

// Keycloak Container (Realm: ecommerce seeded via realm-export.json)
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.6.4")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithBindMount(Path.GetFullPath("realm-export.json"), "/opt/keycloak/data/import/realm-export.json")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithArgs("start-dev", "--import-realm");

// Microservices
var orderingApi = builder.AddProject("ordering-api", "../../Services/Ordering/Ordering.Api/Ordering.Api.csproj")
    .WithReference(orderingDb)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitFor(postgres)
    .WaitFor(rabbitmq);

var inventoryApi = builder.AddProject("inventory-api", "../../Services/Inventory/Inventory.Api/Inventory.Api.csproj")
    .WithReference(inventoryDb)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitFor(postgres)
    .WaitFor(rabbitmq);

var paymentsApi = builder.AddProject("payments-api", "../../Services/Payments/Payments.Api/Payments.Api.csproj")
    .WithReference(paymentsDb)
    .WithReference(rabbitmq)
    .WaitFor(postgres)
    .WaitFor(rabbitmq);

var fulfillmentApi = builder.AddProject("fulfillment-api", "../../Services/Fulfillment/Fulfillment.Api/Fulfillment.Api.csproj")
    .WithReference(fulfillmentDb)
    .WithReference(rabbitmq)
    .WaitFor(postgres)
    .WaitFor(rabbitmq);

// YARP Gateway
builder.AddProject("gateway", "../../Gateways/ECommerce.Gateway/ECommerce.Gateway.csproj")
    .WithReference(redis)
    .WithReference(orderingApi)
    .WithReference(inventoryApi)
    .WithReference(paymentsApi)
    .WithReference(fulfillmentApi)
    .WaitFor(orderingApi);

builder.Build().Run();


