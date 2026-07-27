using Microsoft.AspNetCore.Http;
using WorkManagementSystem.API.Middlewares;

namespace WorkManagementSystem.Tests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithValidClientCorrelationId_UsesItAcrossRequestAndResponse()
    {
        const string correlationId = "client-request-123";
        string? traceIdentifierInsidePipeline = null;
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;

        var middleware = new CorrelationIdMiddleware(nextContext =>
        {
            traceIdentifierInsidePipeline = nextContext.TraceIdentifier;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(correlationId, traceIdentifierInsidePipeline);
        Assert.Equal(correlationId, context.TraceIdentifier);
        Assert.Equal(
            correlationId,
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_WithUnsafeClientCorrelationId_KeepsServerTraceIdentifier()
    {
        var serverTraceIdentifier = Guid.NewGuid().ToString("N");
        var context = new DefaultHttpContext
        {
            TraceIdentifier = serverTraceIdentifier
        };
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = new string('x', 65);

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(serverTraceIdentifier, context.TraceIdentifier);
        Assert.Equal(
            serverTraceIdentifier,
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }
}
