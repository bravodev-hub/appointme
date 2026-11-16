using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AppointMe.Shared.Configuration;
using AppointMe.Shared.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace AppointMe.Api.Authentication.DemoLogin;

internal sealed partial class DemoLoginEndpoint : IEndpoint
{
    private const string DemoAuthenticationType = "DemoLogin";

    private static readonly string[] RelevantClaimTypes =
        ["sub", "oid", "name", "given_name", "family_name", "email", "emails", "preferred_username"];

    private static readonly string[] NameClaimTypes = ["name", "given_name", "family_name", "preferred_username"];
    private static readonly string[] EmailClaimTypes = ["email", "emails", "preferred_username"];

    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/login/demo", DemoLogin)
            .AllowAnonymous()
            .WithName(nameof(DemoLogin))
            .ExcludeFromDescription();
    }

    private static async Task<IResult> DemoLogin(
        HttpContext context,
        IOptions<DemoOptions> demoOptions,
        IOptions<FrontendOptions> frontendOptions,
        IDemoUserAuthenticator authenticator,
        ILogger<DemoLoginEndpoint> logger,
        CancellationToken cancellationToken)
    {
        if (!demoOptions.Value.Enabled || demoOptions.Value.User is not { } demoUser)
        {
            return TypedResults.NotFound();
        }

        var idToken = await authenticator.AuthenticateAsync(demoUser.Email, demoUser.Password, cancellationToken);
        if (idToken is null)
        {
            LogAuthenticationFailed(logger, demoUser.Email);
            return TypedResults.Problem("Demo login failed.", statusCode: StatusCodes.Status502BadGateway);
        }

        var principal = BuildPrincipal(idToken, demoUser);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return TypedResults.Redirect(frontendOptions.Value.BaseUrl.ToString());
    }

    private static ClaimsPrincipal BuildPrincipal(string idToken, DemoUserOptions demoUser)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
        var claims = jwt.Claims.Where(claim => RelevantClaimTypes.Contains(claim.Type)).ToList();

        if (!claims.Exists(claim => NameClaimTypes.Contains(claim.Type)))
        {
            claims.Add(new Claim("name", string.IsNullOrWhiteSpace(demoUser.Name) ? "Demo User" : demoUser.Name));
        }

        if (!claims.Exists(claim => EmailClaimTypes.Contains(claim.Type)))
        {
            claims.Add(new Claim("email", demoUser.Email));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, DemoAuthenticationType));
    }

    [LoggerMessage(LogLevel.Warning, "Demo login failed to authenticate {Email} against the identity provider.")]
    private static partial void LogAuthenticationFailed(ILogger logger, string email);
}
