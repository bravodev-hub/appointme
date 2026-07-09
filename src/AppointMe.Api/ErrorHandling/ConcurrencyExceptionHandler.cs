using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppointMe.Api.ErrorHandling;

internal sealed class ConcurrencyExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ConcurrencyExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        // Wolverine may surface the original as an inner exception; check both.
        if (exception is not DbUpdateConcurrencyException
            && exception.InnerException is not DbUpdateConcurrencyException)
        {
            return false;
        }

        logger.LogWarning(exception, "Optimistic concurrency conflict");

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "Conflict",
                Detail = "The record was modified by another operation. Reload and try again.",
                Status = StatusCodes.Status409Conflict
            }
        };

        context.ProblemDetails.Extensions["code"] = "concurrency_conflict";

        return await problemDetailsService.TryWriteAsync(context);
    }
}
