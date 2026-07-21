namespace AppointMe.Api.SecurityHeaders;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    // Report-only while the policy is tuned against the SPA: chart.tsx injects an inline
    // <style> element and Recharts/React set inline style attributes, hence 'unsafe-inline'
    // for styles. Watch browser-console CSP violation reports, then rename the header to
    // Content-Security-Policy to enforce.
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var headers = ((HttpContext)state).Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Content-Security-Policy-Report-Only"] = ContentSecurityPolicy;

            return Task.CompletedTask;
        }, context);

        return next(context);
    }
}
