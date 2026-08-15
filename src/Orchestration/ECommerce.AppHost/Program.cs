Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

var builder = DistributedApplication.CreateBuilder(args);
// Docker Compose is generated from this topology through `aspire publish`.
// Keep this resource declarative so Compose cannot drift from the AppHost.
builder.AddDockerComposeEnvironment("compose");

// Publish mode is not run mode either; using IsRunMode here caused production
// Compose output to disable issuer validation and omit Keycloak. Tests opt in
// explicitly through their environment name.
var isTestMode = string.Equals(builder.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase)
    || string.Equals(builder.Environment.EnvironmentName, "IntegrationTesting", StringComparison.OrdinalIgnoreCase);

// Infrastructure Containers
var dbRuntimePassword = builder.AddParameter("db-runtime-password", secret: true);
var dbMigrationPassword = builder.AddParameter("db-migration-password", secret: true);
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("18.4")
    .WithDataVolume()
    .WithEnvironment("DB_RUNTIME_PASSWORD", dbRuntimePassword)
    .WithEnvironment("DB_MIGRATION_PASSWORD", dbMigrationPassword)
    .WithBindMount(
        Path.GetFullPath(Path.Combine("..", "..", "..", "infra", "postgres", "init-databases.sh")),
        "/docker-entrypoint-initdb.d/001-databases.sh");

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
    .WithImageTag("4.3.1-management")
    .WithDataVolume();

// Only add management UI in interactive run mode
if (!isTestMode)
    rabbitmq.WithManagementPlugin().WithExternalHttpEndpoints();

// Valkey 9.1 (BSD-3-Clause)
var valkey = builder.AddValkey("valkey")
    .WithImageTag("9.1");

// Keycloak Container (Realm: ecommerce seeded via realm-export.json)
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
var keycloakAdminClientSecret = builder.AddParameter("keycloak-admin-client-secret", secret: true);
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.6.4")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", keycloakAdminPassword)
    .WithBindMount(Path.GetFullPath("realm-export.json"), "/opt/keycloak/data/import/realm-export.json")
    .WithVolume("keycloak-data", "/opt/keycloak/data")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithExternalHttpEndpoints()
    .WithArgs("start-dev", "--import-realm");

const string keycloakAuthority = "http://keycloak:8080/realms/ecommerce";
var validateIssuer = isTestMode ? "false" : "true";

// Microservices — no WaitFor in test mode to avoid blocking StartAsync indefinitely.
// Services handle DB unavailability via EnsureCreated retry in their Program.cs.
var OrderApi = builder.AddProject<Projects.Order_Api>("Order-api")
    .WithReference(OrderDb)
    .WithReference(rabbitmq)
    .WithReference(valkey)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithEnvironment("ConnectionStrings__OrderDb", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=Order_db;Username=order_runtime;Password={dbRuntimePassword}"))
    .WithEnvironment("ConnectionStrings__OrderDb_Migration", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=Order_db;Username=order_migration;Password={dbMigrationPassword}"));

if (!isTestMode)
    OrderApi.WaitFor(postgres).WaitFor(rabbitmq).WaitFor(valkey);

var inventoryApi = builder.AddProject<Projects.Inventory_Api>("inventory-api")
    .WithReference(inventoryDb)
    .WithReference(rabbitmq)
    .WithReference(valkey)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithEnvironment("InventoryReservation__LeaseDuration", "00:02:00")
    .WithEnvironment("InventoryReservation__ReaperInterval", "00:00:15")
    .WithEnvironment("InventoryReservation__ReaperBatchSize", "100")
    .WithEnvironment("ConnectionStrings__InventoryDb", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=inventory_db;Username=inventory_runtime;Password={dbRuntimePassword}"))
    .WithEnvironment("ConnectionStrings__InventoryDb_Migration", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=inventory_db;Username=inventory_migration;Password={dbMigrationPassword}"));

if (!isTestMode)
    inventoryApi.WaitFor(postgres).WaitFor(rabbitmq).WaitFor(valkey);

var PaymentApi = builder.AddProject<Projects.Payment_Api>("Payment-api")
    .WithReference(PaymentDb)
    .WithReference(rabbitmq)
    .WithReference(valkey)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithEnvironment("ConnectionStrings__PaymentDb", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=Payment_db;Username=payment_runtime;Password={dbRuntimePassword}"))
    .WithEnvironment("ConnectionStrings__PaymentDb_Migration", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=Payment_db;Username=payment_migration;Password={dbMigrationPassword}"));

if (!isTestMode)
    PaymentApi.WaitFor(postgres).WaitFor(rabbitmq).WaitFor(valkey);

var fulfillmentApi = builder.AddProject<Projects.Fulfillment_Api>("fulfillment-api")
    .WithReference(fulfillmentDb)
    .WithReference(rabbitmq)
    .WithReference(valkey)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithEnvironment("ConnectionStrings__FulfillmentDb", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=fulfillment_db;Username=fulfillment_runtime;Password={dbRuntimePassword}"))
    .WithEnvironment("ConnectionStrings__FulfillmentDb_Migration", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=fulfillment_db;Username=fulfillment_migration;Password={dbMigrationPassword}"));

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
    .WithEnvironment("Keycloak__AdminClientSecret", keycloakAdminClientSecret)
    .WithEnvironment("ConnectionStrings__IamDb", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=iam_db;Username=iam_runtime;Password={dbRuntimePassword}"))
    .WithEnvironment("ConnectionStrings__IamDb_Migration", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=iam_db;Username=iam_migration;Password={dbMigrationPassword}"));

if (!isTestMode)
    iamApi.WaitFor(postgres).WaitFor(rabbitmq).WaitFor(valkey).WaitFor(keycloak);

var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithEnvironment("ConnectionStrings__CatalogDb", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=catalog_db;Username=catalog_runtime;Password={dbRuntimePassword}"))
    .WithEnvironment("ConnectionStrings__CatalogDb_Migration", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=catalog_db;Username=catalog_migration;Password={dbMigrationPassword}"));

if (!isTestMode)
    catalogApi.WaitFor(postgres).WaitFor(rabbitmq);

var customerApi = builder.AddProject<Projects.Customer_Api>("customer-api")
    .WithReference(customerDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithEnvironment("ConnectionStrings__CustomerDb", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=customer_db;Username=customer_runtime;Password={dbRuntimePassword}"))
    .WithEnvironment("ConnectionStrings__CustomerDb_Migration", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=customer_db;Username=customer_migration;Password={dbMigrationPassword}"));

if (!isTestMode)
    customerApi.WaitFor(postgres).WaitFor(rabbitmq);

var searchApi = builder.AddProject<Projects.Search_Api>("search-api")
    .WithReference(searchDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithEnvironment("ConnectionStrings__SearchDb", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=search_db;Username=search_runtime;Password={dbRuntimePassword}"))
    .WithEnvironment("ConnectionStrings__SearchDb_Migration", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=search_db;Username=search_migration;Password={dbMigrationPassword}"));

if (!isTestMode)
    searchApi.WaitFor(postgres).WaitFor(rabbitmq);

var notificationApi = builder.AddProject<Projects.Notification_Api>("notification-api")
    .WithReference(notificationDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithEnvironment("ConnectionStrings__NotificationDb", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=notification_db;Username=notification_runtime;Password={dbRuntimePassword}"))
    .WithEnvironment("ConnectionStrings__NotificationDb_Migration", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=notification_db;Username=notification_migration;Password={dbMigrationPassword}"));

if (!isTestMode)
    notificationApi.WaitFor(postgres).WaitFor(rabbitmq);

var promotionApi = builder.AddProject<Projects.Promotion_Api>("promotion-api")
    .WithReference(promotionDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithEnvironment("ConnectionStrings__PromotionDb", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=promotion_db;Username=promotion_runtime;Password={dbRuntimePassword}"))
    .WithEnvironment("ConnectionStrings__PromotionDb_Migration", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=promotion_db;Username=promotion_migration;Password={dbMigrationPassword}"));

if (!isTestMode)
    promotionApi.WaitFor(postgres).WaitFor(rabbitmq);

var AuditApi = builder.AddProject<Projects.Audit_Api>("Audit-api")
    .WithReference(AuditDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Authority", keycloakAuthority)
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithEnvironment("ConnectionStrings__AuditDb", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=Audit_db;Username=audit_runtime;Password={dbRuntimePassword}"))
    .WithEnvironment("ConnectionStrings__AuditDb_Migration", Aspire.Hosting.ApplicationModel.ReferenceExpression.Create($"Host=postgres;Port=5432;Database=Audit_db;Username=audit_migration;Password={dbMigrationPassword}"));

if (!isTestMode)
    AuditApi.WaitFor(postgres).WaitFor(rabbitmq);

// gRPC service discovery references
OrderApi
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
    .WithEnvironment("Jwt__ValidateIssuer", validateIssuer)
    .WithExternalHttpEndpoints();

if (!isTestMode)
    gateway.WaitFor(OrderApi).WaitFor(valkey).WaitFor(keycloak);

builder.Build().Run();
