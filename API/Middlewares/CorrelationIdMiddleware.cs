using Serilog.Context;

namespace WorkManagementSystem.API.Middlewares
{
    public sealed class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-ID";
        private const int MaxCorrelationIdLength = 64;

        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = ResolveCorrelationId(context);
            context.TraceIdentifier = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }

        private static string ResolveCorrelationId(HttpContext context)
        {
            var values = context.Request.Headers[HeaderName];
            if (values.Count == 1 && IsValid(values[0]))
                return values[0]!;

            return string.IsNullOrWhiteSpace(context.TraceIdentifier)
                ? Guid.NewGuid().ToString("N")
                : context.TraceIdentifier;
        }

        private static bool IsValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxCorrelationIdLength)
                return false;

            return value.All(character =>
                char.IsAsciiLetterOrDigit(character)
                || character is '-' or '_' or '.');
        }
    }
}
