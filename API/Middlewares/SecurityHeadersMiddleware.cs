namespace WorkManagementSystem.API.Middlewares
{
    public sealed class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.Append("Referrer-Policy", "no-referrer");
            headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

            if (context.Request.Path.StartsWithSegments("/api"))
                headers.CacheControl = "no-store";

            await _next(context);
        }
    }
}
