using System.Net;
using System.Text.Json;
using AppointMe.Api.Authentication.EntraExternalId;
using Microsoft.Extensions.Options;

namespace AppointMe.Api.Authentication.DemoLogin;

/// <summary>
/// Demo login for Microsoft Entra External ID via the native authentication API
/// (<c>initiate</c> → <c>challenge</c> → <c>token</c>). Entra External ID does not support ROPC, so
/// this is the only way to sign the demo user in with email + password without a browser redirect.
/// The configured <see cref="EntraExternalIdOptions.ClientId"/> app must be a public client with
/// native authentication (email + password) enabled.
/// </summary>
internal sealed partial class EntraExternalIdDemoUserAuthenticator(
    HttpClient httpClient,
    IOptions<EntraExternalIdOptions> options,
    ILogger<EntraExternalIdDemoUserAuthenticator> logger
) : IDemoUserAuthenticator
{
    // "redirect" must always be included; it tells Entra to signal browser fallback when native
    // password auth isn't available — which for this server-side flow we treat as a failure.
    private const string ChallengeTypes = "password redirect";

    // The native-auth API uses snake_case fields (continuation_token, challenge_type, id_token).
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<string?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken)
    {
        var entra = options.Value;
        var clientId = entra.ClientId;
        var baseUrl = BuildNativeAuthBaseUrl(entra.Authority);

        // 1) initiate — start the sign-in flow for the username.
        var initiate = await PostFormAsync<NativeAuthResponse>(baseUrl + "initiate", new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["challenge_type"] = ChallengeTypes,
            ["username"] = email
        }, cancellationToken);
        if (initiate?.ContinuationToken is not { } initiateToken)
        {
            return null;
        }

        // 2) challenge — Entra selects the authentication method; we require "password".
        var challenge = await PostFormAsync<NativeAuthResponse>(baseUrl + "challenge", new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["challenge_type"] = ChallengeTypes,
            ["continuation_token"] = initiateToken
        }, cancellationToken);
        if (challenge is null)
        {
            return null;
        }

        if (!string.Equals(challenge.ChallengeType, "password", StringComparison.OrdinalIgnoreCase))
        {
            LogUnexpectedChallenge(challenge.ChallengeType);
            return null;
        }

        // 3) token — submit the password and obtain the tokens.
        var token = await PostFormAsync<TokenResponse>(baseUrl + "token", new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["continuation_token"] = challenge.ContinuationToken ?? initiateToken,
            ["grant_type"] = "password",
            ["password"] = password,
            ["scope"] = "openid profile email"
        }, cancellationToken);

        return token?.IdToken;
    }

    private async Task<TResponse?> PostFormAsync<TResponse>(
        string url, Dictionary<string, string> form, CancellationToken cancellationToken)
        where TResponse : class
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await httpClient.PostAsync(url, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogNativeAuthCallFailed(url, response.StatusCode, body);
            return null;
        }

        return JsonSerializer.Deserialize<TResponse>(body, SerializerOptions);
    }

    /// <summary>
    /// Derives the native-auth endpoint base from the OIDC authority, e.g.
    /// <c>https://{tenant}.ciamlogin.com/{tenant-id}/v2.0</c> → <c>https://{tenant}.ciamlogin.com/{tenant-id}/oauth2/v2.0/</c>.
    /// </summary>
    private static string BuildNativeAuthBaseUrl(string authority)
    {
        var trimmed = authority.TrimEnd('/');
        const string versionSuffix = "/v2.0";
        if (trimmed.EndsWith(versionSuffix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^versionSuffix.Length];
        }

        return trimmed + "/oauth2/v2.0/";
    }

    /// <summary>Shared shape of the <c>initiate</c> and <c>challenge</c> responses.</summary>
    private sealed record NativeAuthResponse(string? ContinuationToken, string? ChallengeType);

    /// <summary>The <c>token</c> response; only the id_token is needed to build the session.</summary>
    private sealed record TokenResponse(string? IdToken);

    [LoggerMessage(LogLevel.Warning,
        "Entra demo login: expected a 'password' challenge but got '{ChallengeType}'. " +
        "Native authentication (email + password) may be disabled for this app registration.")]
    private partial void LogUnexpectedChallenge(string? challengeType);

    [LoggerMessage(LogLevel.Warning, "Entra native auth call to {Url} failed: {Status} {Body}")]
    private partial void LogNativeAuthCallFailed(string url, HttpStatusCode status, string body);
}
