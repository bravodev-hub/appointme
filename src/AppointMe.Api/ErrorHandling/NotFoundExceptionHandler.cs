namespace AppointMe.Api.ErrorHandling;

using Shared.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

internal sealed partial class NotFoundExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<NotFoundExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not NotFoundException notFoundException)
        {
            return false;
        }

        LogNotFound(exception);

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "Resource Not Found",
                Detail = notFoundException.Message,
                Status = StatusCodes.Status404NotFound
            }
        };

        return await problemDetailsService.TryWriteAsync(context);
    }

    [LoggerMessage(LogLevel.Warning, "Resource not found")]
    private partial void LogNotFound(Exception exception);
}
