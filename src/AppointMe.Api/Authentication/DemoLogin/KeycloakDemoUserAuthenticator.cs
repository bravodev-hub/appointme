using System.Net;
using System.Text.Json;

namespace AppointMe.Api.Authentication.DemoLogin;

/// <summary>
/// Demo login for Keycloak via the OAuth2 Resource Owner Password Credentials (ROPC) grant.
/// Reuses the configured OIDC client; the realm client must have "Direct Access Grants" enabled.
/// </summary>
internal sealed partial class KeycloakDemoUserAuthenticator(
    HttpClient httpClient,
    IdentityProviderOptions providerOptions,
    ILogger<KeycloakDemoUserAuthenticator> logger
) : IDemoUserAuthenticator
{
    public async Task<string?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken)
    {
        var tokenEndpoint = $"{providerOptions.Authority.TrimEnd('/')}/protocol/openid-connect/token";

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = providerOptions.OidcClientId,
            ["client_secret"] = providerOptions.OidcClientSecret,
            ["username"] = email,
            ["password"] = password,
            ["scope"] = "openid profile email"
        });

        using var response = await httpClient.PostAsync(tokenEndpoint, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogLoginFailed(response.StatusCode, body);
            return null;
        }

        using var json = JsonDocument.Parse(body);
        return json.RootElement.TryGetProperty("id_token", out var idToken) ? idToken.GetString() : null;
    }

    [LoggerMessage(LogLevel.Warning, "Keycloak demo login (ROPC) failed: {Status} {Body}")]
    private partial void LogLoginFailed(HttpStatusCode status, string body);
}
