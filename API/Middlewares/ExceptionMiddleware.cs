using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WorkManagementSystem.Application.Exceptions;

namespace WorkManagementSystem.API.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHostEnvironment _environment;

        public ExceptionMiddleware(RequestDelegate next, IHostEnvironment environment)
        {
            _next = next;
            _environment = environment;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                Log.Debug(
                    "Request was cancelled by the client. TraceId: {TraceId}",
                    context.TraceIdentifier);
            }
            catch (Exception ex)
            {
                if (IsExpectedClientError(ex))
                    Log.Warning(ex, "Request failed with a client/business error: {Message}", ex.Message);
                else
                    Log.Error(ex, "Unexpected server error: {Message}", ex.Message);

                await HandleExceptionAsync(context, ex, _environment.IsDevelopment());
            }
        }

        private static bool IsExpectedClientError(Exception exception)
        {
            return exception is ApiException
                or DbUpdateConcurrencyException
                or UnauthorizedAccessException
                or ArgumentException
                or BadHttpRequestException
                || IsUniqueConstraintViolation(exception);
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception, bool includeDetails)
        {
            context.Response.ContentType = "application/json";

            var error = CreateError(context, exception, includeDetails);
            context.Response.StatusCode = error.StatusCode;

            var response = new
            {
                message = error.Message,
                code = error.Code,
                traceId = error.TraceId,
                details = error.Details,
                errors = error.Errors
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }

        private static ApiError CreateError(HttpContext context, Exception exception, bool includeDetails)
        {
            if (exception is ApiException apiException)
            {
                return new ApiError(
                    apiException.StatusCode,
                    apiException.Code,
                    apiException.Message,
                    includeDetails ? exception.ToString() : string.Empty,
                    context.TraceIdentifier,
                    apiException.Errors);
            }

            if (exception is DbUpdateConcurrencyException)
            {
                return new ApiError(
                    (int)HttpStatusCode.Conflict,
                    "concurrency_conflict",
                    "Du lieu da duoc thay doi boi mot yeu cau khac. Vui long tai lai va thu lai.",
                    includeDetails ? exception.ToString() : string.Empty,
                    context.TraceIdentifier);
            }

            if (IsUniqueConstraintViolation(exception))
            {
                return new ApiError(
                    (int)HttpStatusCode.Conflict,
                    "duplicate_data",
                    "Du lieu da ton tai hoac vua duoc tao boi mot yeu cau khac.",
                    includeDetails ? exception.ToString() : string.Empty,
                    context.TraceIdentifier);
            }

            if (exception is UnauthorizedAccessException)
            {
                return new ApiError(
                    (int)HttpStatusCode.Forbidden,
                    "forbidden",
                    exception.Message,
                    string.Empty,
                    context.TraceIdentifier);
            }

            if (exception is BadHttpRequestException badRequestEx &&
                badRequestEx.Message.Contains("Request body too large", StringComparison.OrdinalIgnoreCase))
            {
                return new ApiError(
                    (int)HttpStatusCode.RequestEntityTooLarge,
                    "request_too_large",
                    "File dinh kem hoac noi dung vuot qua gioi han may chu.",
                    includeDetails ? exception.Message : string.Empty,
                    context.TraceIdentifier);
            }

            if (exception is ArgumentException)
            {
                return new ApiError(
                    (int)HttpStatusCode.BadRequest,
                    "bad_request",
                    exception.Message,
                    string.Empty,
                    context.TraceIdentifier);
            }

            return new ApiError(
                (int)HttpStatusCode.InternalServerError,
                "internal_server_error",
                "Loi he thong noi bo. Vui long thu lai sau.",
                includeDetails ? exception.ToString() : string.Empty,
                context.TraceIdentifier);
        }

        private static bool IsUniqueConstraintViolation(Exception exception)
        {
            var current = exception;
            while (current != null)
            {
                if (current is SqlException sqlException && sqlException.Number is 2601 or 2627)
                    return true;

                current = current.InnerException;
            }

            return false;
        }

        private sealed record ApiError(
            int StatusCode,
            string Code,
            string Message,
            string Details,
            string TraceId,
            IReadOnlyDictionary<string, string[]>? Errors = null);
    }
}
