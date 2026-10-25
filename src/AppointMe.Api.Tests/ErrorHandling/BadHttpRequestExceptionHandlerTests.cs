using System.Text.Json;
using AppointMe.Api.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AppointMe.Api.Tests.ErrorHandling;

public class BadHttpRequestExceptionHandlerTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status413PayloadTooLarge)]
    public async Task should_respond_with_status_code_carried_by_exception(int statusCode)
    {
        var context = CreateContext();
        var handler = CreateHandler();

        var handled = await handler.TryHandleAsync(context,
            new BadHttpRequestException("Required parameter \"int Limit\" was not provided.", statusCode),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(statusCode, context.Response.StatusCode);
    }

    [Fact]
    public async Task should_write_problem_details_with_exception_message()
    {
        var context = CreateContext();
        var handler = CreateHandler();

        await handler.TryHandleAsync(context,
            new BadHttpRequestException("Failed to bind parameter \"int Limit\" from \"abc\"."),
            CancellationToken.None);

        context.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Failed to bind parameter \"int Limit\" from \"abc\".",
            problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task should_not_handle_other_exceptions()
    {
        var context = CreateContext();
        var handler = CreateHandler();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"),
            CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static DefaultHttpContext CreateContext()
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() }
        };
    }

    private static BadHttpRequestExceptionHandler CreateHandler()
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return new BadHttpRequestExceptionHandler(
            services.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>(),
            NullLogger<BadHttpRequestExceptionHandler>.Instance);
    }
}
