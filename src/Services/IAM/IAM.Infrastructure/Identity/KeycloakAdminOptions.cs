namespace IAM.Infrastructure.Identity;

public sealed class KeycloakAdminOptions
{
    public const string SectionName = "Keycloak:Admin";

    public string BaseUrl { get; set; } = "http://keycloak:8080";
    public string Realm { get; set; } = "ecommerce";
    public string ClientId { get; set; } = "ecommerce-admin";
    public string ClientSecret { get; set; } = string.Empty;
}
