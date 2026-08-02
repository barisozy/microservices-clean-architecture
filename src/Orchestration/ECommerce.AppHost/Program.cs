Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

var builder = DistributedApplication.CreateBuilder(args);

// Detect test mode: DistributedApplicationTestingBuilder sets IsRunMode=false
var isTestMode = !builder.ExecutionContext.IsRunMode;

// Infrastructure Containers
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("18.4");

var OrderDb = postgres.AddDatabase("OrderDb", "Order_db");
var inventoryDb = postgres.AddDatabase("InventoryDb", "inventory_db");
var PaymentDb = postgres.AddDatabase("PaymentDb", "Payment_db");
var fulfillmentDb = postgres.AddDatabase("FulfillmentDb", "fulfillment_db");
var iamDb = postgres.AddDatabase("IamDb", "iam_db");
var catalogDb = postgres.AddDatabase("CatalogDb", "catalog_db");
var customerDb = postgres.AddDatabase("CustomerDb", "customer_db");
var searchDb = postgres.AddDatabase("SearchDb", "search_db");
var notificationDb = postgres.AddDatabase("NotificationDb", "notification_db");
var promotionDb = postgres.AddDatabase("PromotionDb", "promotion_db");
var AuditDb = postgres.AddDatabase("AuditDb", "Audit_db");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithImageTag("4.3.1-management");

// Only add management UI in interactive run mode
if (!isTestMode)
    rabbitmq.WithManagementPlugin();

// Valkey 9.1 (BSD-3-Clause)
var valkey = builder.AddValkey("valkey")
    .WithImageTag("9.1");

// Keycloak Container (Realm: ecommerce seeded via realm-export.json)
// Only start Keycloak in interactive run mode — tests use Jwt:ValidateIssuer=false
if (!isTestMode)
{
    builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.6.4")
        .WithEnvironment("KEYCLOAK_ADMIN", "admin")
        .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
        .WithBindMount(Path.GetFullPath("realm-export.json"), "/opt/keycloak/data/import/realm-export.json")
        .WithHttpEndpoint(targetPort: 8080, name: "http")
        .WithArgs("start-dev", "--import-realm");
}

const string keycloakAuthority = "http://keycloak:8080/realms/ecommerce";
var validateIssuer = isTestMode ? "false" : "true";

// Microservices — no WaitFor in test mode to avoid blocking StartAsync indefinitely.
// Services handle DB unavailability via EnsureCreated retry in their Program.cs.
var OrderApi = builder.AddProject<Projects.Order_Api>("Order-api")
    .WithReference(OrderDb)
    .WithReference(rabbitmq)
    .WithReference(valkey)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer);

if (!isTestMode)
    OrderApi.WaitFor(postgres).WaitFor(rabbitmq).WaitFor(valkey);

var inventoryApi = builder.AddProject<Projects.Inventory_Api>("inventory-api")
    .WithReference(inventoryDb)
    .WithReference(rabbitmq)
    .WithReference(valkey)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer);

if (!isTestMode)
    inventoryApi.WaitFor(postgres).WaitFor(rabbitmq).WaitFor(valkey);

var PaymentApi = builder.AddProject<Projects.Payment_Api>("Payment-api")
    .WithReference(PaymentDb)
    .WithReference(rabbitmq)
    .WithReference(valkey)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer);

if (!isTestMode)
    PaymentApi.WaitFor(postgres).WaitFor(rabbitmq).WaitFor(valkey);

var fulfillmentApi = builder.AddProject<Projects.Fulfillment_Api>("fulfillment-api")
    .WithReference(fulfillmentDb)
    .WithReference(rabbitmq)
    .WithReference(valkey)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer);

if (!isTestMode)
    fulfillmentApi.WaitFor(postgres).WaitFor(rabbitmq).WaitFor(valkey);

var iamApi = builder.AddProject<Projects.IAM_Api>("iam-api")
    .WithReference(iamDb)
    .WithReference(rabbitmq)
    .WithReference(valkey)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithEnvironment("Keycloak__BaseUrl", "http://keycloak:8080")
    .WithEnvironment("Keycloak__Realm", "ecommerce")
    .WithEnvironment("Keycloak__AdminClientId", "ecommerce-admin")
    .WithEnvironment("Keycloak__AdminClientSecret", "dev-only-change-me");

if (!isTestMode)
    iamApi.WaitFor(postgres).WaitFor(rabbitmq).WaitFor(valkey);

var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer);

if (!isTestMode)
    catalogApi.WaitFor(postgres).WaitFor(rabbitmq);

var customerApi = builder.AddProject<Projects.Customer_Api>("customer-api")
    .WithReference(customerDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer);

if (!isTestMode)
    customerApi.WaitFor(postgres).WaitFor(rabbitmq);

var searchApi = builder.AddProject<Projects.Search_Api>("search-api")
    .WithReference(searchDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer);

if (!isTestMode)
    searchApi.WaitFor(postgres).WaitFor(rabbitmq);

var notificationApi = builder.AddProject<Projects.Notification_Api>("notification-api")
    .WithReference(notificationDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer);

if (!isTestMode)
    notificationApi.WaitFor(postgres).WaitFor(rabbitmq);

var promotionApi = builder.AddProject<Projects.Promotion_Api>("promotion-api")
    .WithReference(promotionDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer);

if (!isTestMode)
    promotionApi.WaitFor(postgres).WaitFor(rabbitmq);

var AuditApi = builder.AddProject<Projects.Audit_Api>("Audit-api")
    .WithReference(AuditDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer);

if (!isTestMode)
    AuditApi.WaitFor(postgres).WaitFor(rabbitmq);

// gRPC service discovery references
OrderApi
    .WithReference(inventoryApi)
    .WithReference(catalogApi)
    .WithReference(promotionApi);

catalogApi.WithReference(iamApi);
customerApi.WithReference(iamApi);
promotionApi.WithReference(iamApi);
AuditApi.WithReference(iamApi);

// YARP Gateway
var gateway = builder.AddProject<Projects.ECommerce_Gateway>("gateway")
    .WithReference(valkey)
    .WithReference(OrderApi)
    .WithReference(inventoryApi)
    .WithReference(PaymentApi)
    .WithReference(fulfillmentApi)
    .WithReference(iamApi)
    .WithReference(catalogApi)
    .WithReference(customerApi)
    .WithReference(searchApi)
    .WithReference(notificationApi)
    .WithReference(promotionApi)
    .WithReference(AuditApi)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer);

if (!isTestMode)
    gateway.WaitFor(OrderApi).WaitFor(valkey);

builder.Build().Run();
