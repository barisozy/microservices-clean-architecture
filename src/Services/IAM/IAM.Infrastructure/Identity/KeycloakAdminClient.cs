using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using IAM.Application.Common.Interfaces;
using IAM.Domain.Entities;
using Microsoft.Extensions.Options;

namespace IAM.Infrastructure.Identity;

public sealed class KeycloakAdminClient(
    HttpClient httpClient,
    IOptions<KeycloakAdminOptions> options) : IKeycloakAdminClient
{
    private readonly KeycloakAdminOptions _options = options.Value;

    public async Task EnsureUserExistsAsync(
        IamProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Email);

        using var existingRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"admin/realms/{Uri.EscapeDataString(_options.Realm)}/users/{profile.KeycloakSubject:D}");
        await AuthorizeAsync(existingRequest, cancellationToken);
        using var existingResponse = await httpClient.SendAsync(existingRequest, cancellationToken);
        if (existingResponse.IsSuccessStatusCode)
        {
            return;
        }

        if (existingResponse.StatusCode != HttpStatusCode.NotFound)
        {
            await ThrowForUnexpectedStatusAsync(existingResponse, cancellationToken);
        }

        using var createRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"admin/realms/{Uri.EscapeDataString(_options.Realm)}/users")
        {
            Content = JsonContent.Create(new
            {
                id = profile.KeycloakSubject.ToString("D"),
                username = profile.Email,
                email = profile.Email,
                firstName = profile.DisplayName,
                enabled = true,
                emailVerified = false,
                realmRoles = new[] { profile.Role }
            })
        };
        await AuthorizeAsync(createRequest, cancellationToken);
        using var createResponse = await httpClient.SendAsync(createRequest, cancellationToken);

        // A conflict means another retry/instance completed the idempotent create.
        if (createResponse.IsSuccessStatusCode || createResponse.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        await ThrowForUnexpectedStatusAsync(createResponse, cancellationToken);
    }

    private async Task AuthorizeAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException("Keycloak Admin client secret is not configured.");
        }

        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"realms/{Uri.EscapeDataString(_options.Realm)}/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret
            })
        };
        using var tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);
        await ThrowForUnexpectedStatusAsync(tokenResponse, cancellationToken);
        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new HttpRequestException("Keycloak returned an empty token response.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    private static async Task ThrowForUnexpectedStatusAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Keycloak Admin API returned {(int)response.StatusCode} ({response.ReasonPhrase}). {detail}",
            null,
            response.StatusCode);
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);
}
