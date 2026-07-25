using AppointMe.Api.Antiforgery;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace AppointMe.Api.Tests.Antiforgery;

public class AntiforgeryMiddlewareTests
{
    [Theory]
    [InlineData("/api/v1/customers")]
    [InlineData("/")]
    public async Task should_issue_readable_request_token_cookie_on_get(string path)
    {
        var context = CreateContext(HttpMethods.Get, path);
        var antiforgery = new FakeAntiforgery();
        var middleware = CreateMiddleware(antiforgery, out _);

        await middleware.InvokeAsync(context);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{AntiforgeryMiddleware.RequestTokenCookieName}=the-request-token", setCookie);
        Assert.DoesNotContain("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task should_not_issue_request_token_cookie_on_unsafe_method()
    {
        var context = CreateContext(HttpMethods.Post, "/api/v1/customers");
        var antiforgery = new FakeAntiforgery();
        var middleware = CreateMiddleware(antiforgery, out _);

        await middleware.InvokeAsync(context);

        Assert.Equal(0, antiforgery.GetAndStoreTokensCalls);
        Assert.Empty(context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task should_validate_unsafe_api_request_without_bearer_header()
    {
        var context = CreateContext(HttpMethods.Post, "/api/v1/customers");
        var antiforgery = new FakeAntiforgery();
        var nextInvoked = false;
        var middleware = CreateMiddleware(antiforgery, out _, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(1, antiforgery.ValidateRequestCalls);
        Assert.True(nextInvoked);
    }

    [Fact]
    public async Task should_reject_unsafe_api_request_when_token_validation_fails()
    {
        var context = CreateContext(HttpMethods.Post, "/api/v1/customers");
        var antiforgery = new FakeAntiforgery { ShouldFailValidation = true };
        var nextInvoked = false;
        var middleware = CreateMiddleware(antiforgery, out var problemDetailsService, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.False(nextInvoked);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.NotNull(problemDetailsService.WrittenContext);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetailsService.WrittenContext.ProblemDetails.Status);
    }

    [Fact]
    public async Task should_skip_validation_when_bearer_token_header_present()
    {
        var context = CreateContext(HttpMethods.Post, "/api/v1/customers");
        context.Request.Headers.Authorization = "Bearer some-jwt";
        var antiforgery = new FakeAntiforgery();
        var nextInvoked = false;
        var middleware = CreateMiddleware(antiforgery, out _, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(0, antiforgery.ValidateRequestCalls);
        Assert.True(nextInvoked);
    }

    [Fact]
    public async Task should_skip_validation_for_non_api_paths()
    {
        var context = CreateContext(HttpMethods.Post, "/admin/jobs/requeue");
        var antiforgery = new FakeAntiforgery();
        var nextInvoked = false;
        var middleware = CreateMiddleware(antiforgery, out _, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(0, antiforgery.ValidateRequestCalls);
        Assert.True(nextInvoked);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    public async Task should_not_validate_safe_methods(string method)
    {
        var context = CreateContext(method, "/api/v1/customers");
        var antiforgery = new FakeAntiforgery();
        var nextInvoked = false;
        var middleware = CreateMiddleware(antiforgery, out _, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(0, antiforgery.ValidateRequestCalls);
        Assert.True(nextInvoked);
    }

    private static AntiforgeryMiddleware CreateMiddleware(
        FakeAntiforgery antiforgery,
        out FakeProblemDetailsService problemDetailsService,
        RequestDelegate? next = null)
    {
        problemDetailsService = new FakeProblemDetailsService();
        return new AntiforgeryMiddleware(next ?? (_ => Task.CompletedTask), antiforgery, problemDetailsService);
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    private class FakeAntiforgery : IAntiforgery
    {
        public int ValidateRequestCalls { get; private set; }
        public int GetAndStoreTokensCalls { get; private set; }
        public bool ShouldFailValidation { get; init; }

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
        {
            GetAndStoreTokensCalls++;
            return new AntiforgeryTokenSet(
                "the-request-token",
                "the-cookie-token",
                "__RequestVerificationToken",
                AntiforgeryMiddleware.RequestTokenHeaderName);
        }

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => throw new NotSupportedException();

        public Task ValidateRequestAsync(HttpContext httpContext)
        {
            ValidateRequestCalls++;
            return ShouldFailValidation
                ? Task.FromException(new AntiforgeryValidationException("The antiforgery token could not be validated."))
                : Task.CompletedTask;
        }

        public void SetCookieTokenAndHeader(HttpContext httpContext) => throw new NotSupportedException();
    }

    private class FakeProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? WrittenContext { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            WrittenContext = context;
            return ValueTask.CompletedTask;
        }
    }
}
