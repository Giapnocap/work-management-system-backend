using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.API.Contracts;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Exceptions;

namespace WorkManagementSystem.API.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(
            RequestDelegate next,
            IHostEnvironment environment,
            ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _environment = environment;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Request was cancelled by the client. TraceId: {TraceId}; UserId: {UserId}",
                    context.TraceIdentifier,
                    GetLogUserId(context));

                if (!context.Response.HasStarted)
                    context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
            catch (Exception ex)
            {
                if (context.Response.HasStarted)
                {
                    _logger.LogWarning(
                        ex,
                        "The response has already started; the exception cannot be converted to an API error. TraceId: {TraceId}; UserId: {UserId}",
                        context.TraceIdentifier,
                        GetLogUserId(context));
                    throw;
                }

                if (IsExpectedClientError(ex))
                {
                    _logger.LogInformation(
                        "Request rejected with {ExceptionType}. TraceId: {TraceId}; UserId: {UserId}",
                        ex.GetType().Name,
                        context.TraceIdentifier,
                        GetLogUserId(context));
                }
                else
                {
                    _logger.LogError(
                        ex,
                        "Unexpected server error. TraceId: {TraceId}; UserId: {UserId}",
                        context.TraceIdentifier,
                        GetLogUserId(context));
                }

                await HandleExceptionAsync(context, ex, _environment.IsDevelopment());
            }
        }

        private static string GetLogUserId(HttpContext context)
        {
            return context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirst(AuthenticationClaimTypes.UserId)?.Value ?? "unknown"
                : "anonymous";
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
            var error = CreateError(exception, includeDetails);
            return ApiProblemDetailsFactory.WriteAsync(
                context,
                error.StatusCode,
                error.Code,
                error.Message,
                error.Details,
                error.Errors,
                context.RequestAborted);
        }

        private static ApiError CreateError(Exception exception, bool includeDetails)
        {
            if (exception is ApiException apiException)
            {
                return new ApiError(
                    apiException.StatusCode,
                    apiException.Code,
                    apiException.Message,
                    includeDetails ? exception.ToString() : string.Empty,
                    apiException.Errors);
            }

            if (exception is DbUpdateConcurrencyException)
            {
                return new ApiError(
                    (int)HttpStatusCode.Conflict,
                    "concurrency_conflict",
                    "Du lieu da duoc thay doi boi mot yeu cau khac. Vui long tai lai va thu lai.",
                    includeDetails ? exception.ToString() : string.Empty);
            }

            if (IsUniqueConstraintViolation(exception))
            {
                return new ApiError(
                    (int)HttpStatusCode.Conflict,
                    "duplicate_data",
                    "Du lieu da ton tai hoac vua duoc tao boi mot yeu cau khac.",
                    includeDetails ? exception.ToString() : string.Empty);
            }

            if (exception is UnauthorizedAccessException)
            {
                return new ApiError(
                    (int)HttpStatusCode.Forbidden,
                    "forbidden",
                    exception.Message,
                    string.Empty);
            }

            if (exception is BadHttpRequestException badRequestEx &&
                badRequestEx.Message.Contains("Request body too large", StringComparison.OrdinalIgnoreCase))
            {
                return new ApiError(
                    (int)HttpStatusCode.RequestEntityTooLarge,
                    "request_too_large",
                    "File dinh kem hoac noi dung vuot qua gioi han may chu.",
                    includeDetails ? exception.Message : string.Empty);
            }

            if (exception is ArgumentException)
            {
                return new ApiError(
                    (int)HttpStatusCode.BadRequest,
                    "bad_request",
                    exception.Message,
                    string.Empty);
            }

            return new ApiError(
                (int)HttpStatusCode.InternalServerError,
                "internal_server_error",
                "Loi he thong noi bo. Vui long thu lai sau.",
                includeDetails ? exception.ToString() : string.Empty);
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
            IReadOnlyDictionary<string, string[]>? Errors = null);
    }
}
