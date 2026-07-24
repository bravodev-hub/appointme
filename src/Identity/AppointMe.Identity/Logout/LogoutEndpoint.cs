using AppointMe.Shared.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace AppointMe.Identity.Logout;

internal sealed class LogoutEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/logout", Logout).WithName(nameof(Logout)).RequireAuthorization();
    }

    private static async Task Logout(HttpContext context, IOptions<FrontendOptions> frontendOptions)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
        {
            RedirectUri = frontendOptions.Value.BaseUrl.ToString()
        });
    }
}
