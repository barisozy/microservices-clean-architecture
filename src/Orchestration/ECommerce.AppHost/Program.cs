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
var redis = builder.AddRedis("valkey")
    .WithImage("valkey/valkey", "9.1");

// Keycloak Container (Realm: ecommerce seeded via realm-export.json)
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.6.4")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithBindMount(Path.GetFullPath("realm-export.json"), "/opt/keycloak/data/import/realm-export.json")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithArgs("start-dev", "--import-realm");

// Microservices
var orderingApi = builder.AddProject<Projects.Ordering_Api>("ordering-api")
    .WithReference(orderingDb)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitFor(postgres)
    .WaitFor(rabbitmq);

var inventoryApi = builder.AddProject<Projects.Inventory_Api>("inventory-api")
    .WithReference(inventoryDb)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitFor(postgres)
    .WaitFor(rabbitmq);

var paymentsApi = builder.AddProject<Projects.Payments_Api>("payments-api")
    .WithReference(paymentsDb)
    .WithReference(rabbitmq)
    .WaitFor(postgres)
    .WaitFor(rabbitmq);

var fulfillmentApi = builder.AddProject<Projects.Fulfillment_Api>("fulfillment-api")
    .WithReference(fulfillmentDb)
    .WithReference(rabbitmq)
    .WaitFor(postgres)
    .WaitFor(rabbitmq);

// YARP Gateway
builder.AddProject<Projects.ECommerce_Gateway>("gateway")
    .WithReference(redis)
    .WithReference(orderingApi)
    .WithReference(inventoryApi)
    .WithReference(paymentsApi)
    .WithReference(fulfillmentApi)
    .WaitFor(orderingApi);

builder.Build().Run();
