using System.Net.Http.Json;
using Xunit;
using Shouldly;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;

namespace ECommerce.IntegrationTests;

public class OrderE2ETest
{
    [Fact]
    public async Task CreateOrder_EndToEnd_SagaScenarios_ShouldSucceed()
    {
        // Arrange - Create HttpClients targeting the docker-compose environment
        var keycloakClient = new HttpClient { BaseAddress = new Uri("http://localhost:8081") };
        var gatewayClient = new HttpClient { BaseAddress = new Uri("http://localhost:8080") };
        
        string token = string.Empty;
        string lastError = string.Empty;
        var timeout = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < timeout)
        {
            try
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", "ecommerce-gateway"),
                    new KeyValuePair<string, string>("username", "demouser"),
                    new KeyValuePair<string, string>("password", "password123"),
                    new KeyValuePair<string, string>("grant_type", "password")
                });

                var response = await keycloakClient.PostAsync("/realms/ecommerce/protocol/openid-connect/token", content, TestContext.Current.CancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
                    token = json.GetProperty("access_token").GetString() ?? "";
                    break;
                }
                else 
                {
                    lastError = $"Status: {response.StatusCode}, Content: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}";
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
            await Task.Delay(5000, TestContext.Current.CancellationToken);
        }

        token.ShouldNotBeNullOrEmpty($"Failed to obtain JWT token from Keycloak. Is Keycloak healthy? Last Error: {lastError}");

        gatewayClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // =========================================================================
        // SCENARIO 1: SUCCESSFUL ORDER CREATION (AND IDEMPOTENCY)
        // =========================================================================
        var idempotencyKey1 = Guid.NewGuid().ToString();
        var successPayload = new
        {
            items = new[]
            {
                new { sku = "PROD-SUCCESS", quantity = 1, unitPrice = 100.0m }
            }
        };

        HttpResponseMessage? orderResponse = null;
        for (int i = 0; i < 60; i++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
                request.Headers.Add("Idempotency-Key", idempotencyKey1);
                request.Content = JsonContent.Create(successPayload);

                orderResponse = await gatewayClient.SendAsync(request, TestContext.Current.CancellationToken);
                
                if (orderResponse.IsSuccessStatusCode)
                    break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gateway Retry {i}/60] Failed: {ex.Message}");
                if (i == 59) throw;
            }
            await Task.Delay(2000, TestContext.Current.CancellationToken);
        }
        
        orderResponse.ShouldNotBeNull();
        orderResponse.IsSuccessStatusCode.ShouldBeTrue($"Expected 2xx but got {orderResponse.StatusCode}");
        
        var successOrderId = await orderResponse.Content.ReadFromJsonAsync<Guid>(cancellationToken: TestContext.Current.CancellationToken);
        successOrderId.ShouldNotBe(Guid.Empty);

        // Verify Idempotency - send exact same request again
        var retryRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
        retryRequest.Headers.Add("Idempotency-Key", idempotencyKey1);
        retryRequest.Content = JsonContent.Create(successPayload);
        var retryResponse = await gatewayClient.SendAsync(retryRequest, TestContext.Current.CancellationToken);
        retryResponse.IsSuccessStatusCode.ShouldBeTrue();
        var retryOrderId = await retryResponse.Content.ReadFromJsonAsync<Guid>(cancellationToken: TestContext.Current.CancellationToken);
        retryOrderId.ShouldBe(successOrderId, "Idempotency failed: expected same OrderId for the same Idempotency-Key.");

        // Poll order status (Success flow ends in Pending in the read model currently)
        await VerifyOrderStatusAsync(gatewayClient, successOrderId, "Pending");

        // =========================================================================
        // SCENARIO 2: PAYMENT FAILURE SAGA (CANCELLATION)
        // =========================================================================
        var idempotencyKey2 = Guid.NewGuid().ToString();
        var failurePayload = new
        {
            items = new[]
            {
                new { sku = "FAIL_PAYMENT_SKU", quantity = 1, unitPrice = 50.0m }
            }
        };

        var failRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
        failRequest.Headers.Add("Idempotency-Key", idempotencyKey2);
        failRequest.Content = JsonContent.Create(failurePayload);

        var failResponse = await gatewayClient.SendAsync(failRequest, TestContext.Current.CancellationToken);
        failResponse.IsSuccessStatusCode.ShouldBeTrue();
        var failedOrderId = await failResponse.Content.ReadFromJsonAsync<Guid>(cancellationToken: TestContext.Current.CancellationToken);
        failedOrderId.ShouldNotBe(Guid.Empty);

        // Wait for the Saga to rollback and Cancel the order
        await VerifyOrderStatusAsync(gatewayClient, failedOrderId, "Cancelled");
    }

    private static async Task VerifyOrderStatusAsync(HttpClient client, Guid orderId, string expectedStatus)
    {
        string currentStatus = string.Empty;
        var timeout = DateTime.UtcNow.AddSeconds(60); // Max wait for saga to complete
        
        while (DateTime.UtcNow < timeout)
        {
            var response = await client.GetAsync($"/api/v1/orders/{orderId}", TestContext.Current.CancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                try
                {
                    var orderDto = JsonSerializer.Deserialize<JsonElement>(content);
                    if (orderDto.TryGetProperty("status", out var statusProp))
                    {
                        currentStatus = statusProp.GetString() ?? "";
                        if (currentStatus.Equals(expectedStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            return; // Success!
                        }
                    }
                }
                catch { }
            }
            await Task.Delay(2000, TestContext.Current.CancellationToken);
        }

        currentStatus.ShouldBe(expectedStatus, $"Order {orderId} did not reach expected status '{expectedStatus}' within timeout. Last known status: '{currentStatus}'");
    }
}
