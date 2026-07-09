namespace AppointMe.Api.ErrorHandling;

internal static class ErrorHandlingServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAppointMeErrorHandling()
        {
            return services
                .AddProblemDetails()
                .AddExceptionHandler<ValidationExceptionHandler>()
                .AddExceptionHandler<NotFoundExceptionHandler>()
                .AddExceptionHandler<ConflictExceptionHandler>()
                .AddExceptionHandler<AccessDeniedExceptionHandler>()
                .AddExceptionHandler<ConcurrencyExceptionHandler>()
                .AddExceptionHandler<GlobalExceptionHandler>();
        }
    }
}
