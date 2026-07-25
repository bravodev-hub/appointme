using AppointMe.Api.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace AppointMe.Api.Antiforgery;

public class AntiforgeryMiddleware(
    RequestDelegate next,
    IAntiforgery antiforgery,
    IProblemDetailsService problemDetailsService)
{
    public const string RequestTokenCookieName = "XSRF-TOKEN";
    public const string RequestTokenHeaderName = "X-XSRF-TOKEN";

    private static readonly PathString ApiPrefix = new("/api");

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSafeMethod(context.Request.Method))
        {
            // Issued on every GET (not just once) because tokens are bound to the authenticated
            // identity — the SPA's first GET after login/logout refreshes the pair automatically.
            if (HttpMethods.IsGet(context.Request.Method))
            {
                IssueRequestTokenCookie(context);
            }

            await next(context);
            return;
        }

        if (RequiresValidation(context.Request))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                await WriteValidationProblem(context);
                return;
            }
        }

        await next(context);
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) ||
        HttpMethods.IsHead(method) ||
        HttpMethods.IsOptions(method) ||
        HttpMethods.IsTrace(method);

    // Bearer requests carry no ambient credential, so they cannot be forged cross-site;
    // only cookie-backed requests need the token pair. Non-/api surfaces protect themselves
    // (Hangfire ships its own antiforgery, the OIDC callback is covered by state/correlation).
    private static bool RequiresValidation(HttpRequest request) =>
        request.Path.StartsWithSegments(ApiPrefix) && !request.HasBearerTokenHeader();

    private void IssueRequestTokenCookie(HttpContext context)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        if (tokens.RequestToken is null)
        {
            return;
        }

        context.Response.Cookies.Append(RequestTokenCookieName, tokens.RequestToken, new CookieOptions
        {
            // Must be readable by the SPA so axios can echo it back in the X-XSRF-TOKEN header.
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }

    private async Task WriteValidationProblem(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Title = "Antiforgery Validation Error",
                Detail = "The request must include a valid antiforgery token",
                Status = StatusCodes.Status400BadRequest
            }
        });
    }
}
