using Serilog.Context;
using WorkManagementSystem.Application.Common;

namespace WorkManagementSystem.API.Middlewares;

public sealed class UserLogContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserLogContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(AuthenticationClaimTypes.UserId)?.Value
            : null;
        if (string.IsNullOrWhiteSpace(userId))
        {
            await _next(context);
            return;
        }

        using (LogContext.PushProperty("UserId", userId))
        {
            await _next(context);
        }
    }
}
