using AppointMe.Shared.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppointMe.Api.ErrorHandling;

internal sealed partial class ConflictExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ConflictExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ConflictException conflictException)
        {
            return false;
        }

        LogConflict(exception);

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "Conflict",
                Detail = "Conflict occurred",
                Status = StatusCodes.Status409Conflict
            }
        };

        context.ProblemDetails.Extensions.Add("error", conflictException.Message);
        if (conflictException.Code is not null)
        {
            context.ProblemDetails.Extensions["code"] = conflictException.Code;
        }

        return await problemDetailsService.TryWriteAsync(context);
    }

    [LoggerMessage(LogLevel.Error, "Conflict occurred")]
    private partial void LogConflict(Exception exception);
}
