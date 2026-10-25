using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppointMe.Api.ErrorHandling;

// Request binding failures (a missing required or unparsable parameter, a malformed JSON body) surface as
// BadHttpRequestException when ThrowOnBadRequest is on, the Development default, and Kestrel throws it in every
// environment for limits such as an oversized body. Without this handler GlobalExceptionHandler reports them as 500.
internal sealed class BadHttpRequestExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<BadHttpRequestExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badHttpRequestException)
        {
            return false;
        }

        logger.LogWarning(exception, "Bad request");

        httpContext.Response.StatusCode = badHttpRequestException.StatusCode;
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "Bad Request",
                Detail = badHttpRequestException.Message,
                Status = badHttpRequestException.StatusCode
            }
        };

        return await problemDetailsService.TryWriteAsync(context);
    }
}
