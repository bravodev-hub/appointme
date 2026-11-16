using AppointMe.Shared.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppointMe.Api.ErrorHandling;

internal sealed partial class ValidationExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ValidationExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        LogValidationFailed(exception);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "Validation Error",
                Detail = "One or more validation errors occurred",
                Status = StatusCodes.Status400BadRequest
            }
        };

        context.ProblemDetails.Extensions.Add("error", validationException.Message);

        return await problemDetailsService.TryWriteAsync(context);
    }

    [LoggerMessage(LogLevel.Error, "Unhandled validation exception occurred")]
    private partial void LogValidationFailed(Exception exception);
}
