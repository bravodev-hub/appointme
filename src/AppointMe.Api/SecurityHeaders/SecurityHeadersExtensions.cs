namespace AppointMe.Api.SecurityHeaders;

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseAppointMeSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
