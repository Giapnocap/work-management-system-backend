using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using WorkManagementSystem.API.Middlewares;

namespace WorkManagementSystem.Tests;

public class ExceptionMiddlewareTests
{
    [Fact]
    public async Task Invoke_WhenConcurrencyConflictOccurs_ReturnsConflictResponse()
    {
        var middleware = new ExceptionMiddleware(
            _ => throw new DbUpdateConcurrencyException("Concurrent update"),
            new TestHostEnvironment());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(
            "concurrency_conflict",
            response.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invoke_WhenClientCancelsRequest_DoesNotWriteServerErrorResponse()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var middleware = new ExceptionMiddleware(
            _ => throw new OperationCanceledException(cancellation.Token),
            new TestHostEnvironment());
        var context = new DefaultHttpContext();
        context.RequestAborted = cancellation.Token;
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    private sealed class TestHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "WorkManagementSystem.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
