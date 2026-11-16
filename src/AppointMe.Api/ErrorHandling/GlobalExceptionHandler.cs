using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppointMe.Api.ErrorHandling;

internal sealed partial class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "Unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError
            }
        };

        return await problemDetailsService.TryWriteAsync(context);
    }

    [LoggerMessage(LogLevel.Error, "Unhandled exception occurred")]
    private partial void LogUnhandledException(Exception exception);
}
