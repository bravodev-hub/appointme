using AppointMe.Shared.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AppointMe.Identity.Login;

internal sealed class LoginEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/login", Login).AllowAnonymous().WithName(nameof(Login));
    }

    private static ChallengeHttpResult Login([FromQuery] string? provider,
        IOptions<FrontendOptions> frontendOptions, [FromQuery] string returnUrl = "/")
    {
        var redirectUri = new UriBuilder(frontendOptions.Value.BaseUrl);
        if (Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            redirectUri.Path = returnUrl;
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUri.ToString()
        };

        if (!string.IsNullOrEmpty(provider))
        {
            properties.Items["provider"] = provider;
        }

        return TypedResults.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
    }
}
