using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using StackExchange.Redis;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace ECommerce.IntegrationTests;

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<InfrastructureFixture>;

/// <summary>
/// Shared production-equivalent infrastructure for integration tests.
/// </summary>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgresContainer { get; } = new PostgreSqlBuilder("postgres:18.4-alpine")
        .WithDatabase("ecommerce")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public RabbitMqContainer RabbitMqContainer { get; } = new RabbitMqBuilder("rabbitmq:4.3.1-management-alpine")
        .WithUsername("test")
        .WithPassword("test")
        .WithPortBinding(15672, true)
        .Build();

    public IContainer ValkeyContainer { get; } = new ContainerBuilder("valkey/valkey:9.1-alpine")
        .WithExposedPort(6379)
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
        .Build();

    public IContainer KeycloakContainer { get; } = new ContainerBuilder("quay.io/keycloak/keycloak:26.6.4")
        .WithPortBinding(18080, 8080)
        .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
        .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
        .WithEnvironment("KC_HOSTNAME", "http://localhost:18080")
        .WithBindMount(GetRealmExportPath(), "/opt/keycloak/data/import/realm-export.json")
        .WithCommand("start-dev", "--import-realm")
        .Build();

    public string PostgresConnectionString => PostgresContainer.GetConnectionString();
    public string RabbitMqConnectionString => RabbitMqContainer.GetConnectionString();
    public string RabbitMqManagementAddress => $"http://{RabbitMqContainer.Hostname}:{RabbitMqContainer.GetMappedPublicPort(15672)}";
    public string ValkeyConnectionString => $"{ValkeyContainer.Hostname}:{ValkeyContainer.GetMappedPublicPort(6379)},abortConnect=false,connectRetry=5,connectTimeout=10000";
    public string KeycloakBaseAddress => "http://localhost:18080";
    public string KeycloakAuthority => $"{KeycloakBaseAddress}/realms/ecommerce";
    public string KeycloakIssuer { get; private set; } = string.Empty;
    public IReadOnlyCollection<SecurityKey> KeycloakSigningKeys { get; private set; } = [];
    public IConnectionMultiplexer ValkeyConnection { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(
            PostgresContainer.StartAsync(),
            RabbitMqContainer.StartAsync(),
            ValkeyContainer.StartAsync(),
            KeycloakContainer.StartAsync());

        ValkeyConnection = await ConnectionMultiplexer.ConnectAsync(ValkeyConnectionString);
        await WaitForKeycloakAsync();
        await LoadKeycloakSigningKeysAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await ValkeyConnection.DisposeAsync();
        await Task.WhenAll(
            PostgresContainer.DisposeAsync().AsTask(),
            RabbitMqContainer.DisposeAsync().AsTask(),
            ValkeyContainer.DisposeAsync().AsTask(),
            KeycloakContainer.DisposeAsync().AsTask());
    }

    public async Task<string> GetCustomerAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = new Uri(KeycloakBaseAddress) };
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "ecommerce-gateway",
            ["username"] = "demouser",
            ["password"] = "password123"
        });
        using var response = await client.PostAsync("/realms/ecommerce/protocol/openid-connect/token", body, cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: cancellationToken);
        var accessToken = token?.AccessToken ?? throw new InvalidOperationException("Keycloak did not return an access token.");
        KeycloakIssuer = ReadIssuer(accessToken);
        return accessToken;
    }

    public async Task<string> GetRabbitMqDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = new Uri(RabbitMqManagementAddress) };
        var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test:test"));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        return await client.GetStringAsync("/api/queues", cancellationToken);
    }

    private async Task WaitForKeycloakAsync()
    {
        using var client = new HttpClient { BaseAddress = new Uri(KeycloakBaseAddress) };
        var deadline = DateTime.UtcNow.AddSeconds(60);
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync("/realms/ecommerce/.well-known/openid-configuration");
                if (response.IsSuccessStatusCode) return;
            }
            catch (Exception exception)
            {
                lastError = exception;
            }
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        throw new TimeoutException("Keycloak did not become ready within 60 seconds.", lastError);
    }

    private async Task LoadKeycloakSigningKeysAsync()
    {
        using var client = new HttpClient { BaseAddress = new Uri(KeycloakBaseAddress) };
        var json = await client.GetStringAsync("/realms/ecommerce/protocol/openid-connect/certs");
        KeycloakSigningKeys = new JsonWebKeySet(json).Keys.Cast<SecurityKey>().ToArray();
        if (KeycloakSigningKeys.Count == 0)
            throw new InvalidOperationException("Keycloak did not expose any signing keys.");
    }

    private static string GetRealmExportPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ECommerce.sln")))
            directory = directory.Parent;

        return directory is null
            ? throw new InvalidOperationException("Could not locate the repository root for the Keycloak realm export.")
            : Path.Combine(directory.FullName, "src", "Orchestration", "ECommerce.AppHost", "realm-export.json");
    }

    private static string ReadIssuer(string accessToken)
    {
        var segments = accessToken.Split('.');
        if (segments.Length < 2)
            throw new InvalidOperationException("Keycloak returned an invalid JWT.");

        var payload = segments[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var document = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(payload));
        return document.RootElement.GetProperty("iss").GetString()
            ?? throw new InvalidOperationException("The Keycloak access token does not contain an issuer.");
    }

    private sealed record KeycloakTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken);
}
