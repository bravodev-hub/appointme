using AppointMe.Api.SecurityHeaders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace AppointMe.Api.Tests.SecurityHeaders;

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task should_set_content_type_options_header_when_response_starts()
    {
        var (context, responseFeature) = CreateContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
    }

    [Fact]
    public async Task should_set_frame_options_header_when_response_starts()
    {
        var (context, responseFeature) = CreateContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
    }

    [Fact]
    public async Task should_set_referrer_policy_header_when_response_starts()
    {
        var (context, responseFeature) = CreateContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"].ToString());
    }

    [Fact]
    public async Task should_set_report_only_content_security_policy_when_response_starts()
    {
        var (context, responseFeature) = CreateContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        var policy = context.Response.Headers["Content-Security-Policy-Report-Only"].ToString();
        Assert.Contains("default-src 'self'", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.Contains("object-src 'none'", policy);
        Assert.True(context.Response.Headers["Content-Security-Policy"].Count == 0,
            "enforcing CSP header must not be emitted while the policy is report-only");
    }

    [Fact]
    public async Task should_apply_headers_after_response_was_cleared_by_error_handling()
    {
        var (context, responseFeature) = CreateContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        context.Response.Clear();
        await responseFeature.FireOnStartingAsync();

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
    }

    [Fact]
    public async Task should_invoke_next_middleware()
    {
        var (context, _) = CreateContext();
        var nextInvoked = false;
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextInvoked);
    }

    private static (HttpContext Context, CapturingResponseFeature ResponseFeature) CreateContext()
    {
        var context = new DefaultHttpContext();
        var responseFeature = new CapturingResponseFeature();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        return (context, responseFeature);
    }

    private class CapturingResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStartingCallbacks = [];

        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) => _onStartingCallbacks.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireOnStartingAsync()
        {
            foreach (var (callback, state) in _onStartingCallbacks)
            {
                await callback(state);
            }

            HasStarted = true;
        }
    }
}
