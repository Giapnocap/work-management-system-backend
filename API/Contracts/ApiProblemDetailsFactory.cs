using Microsoft.AspNetCore.Mvc;

namespace WorkManagementSystem.API.Contracts;

public static class ApiProblemDetailsFactory
{
    private const string ErrorTypeBase = "https://httpstatuses.com/";

    public static ApiProblemDetails Create(
        HttpContext context,
        int status,
        string code,
        string message,
        string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        return new ApiProblemDetails
        {
            Type = $"{ErrorTypeBase}{status}",
            Title = message,
            Status = status,
            Detail = detail ?? string.Empty,
            Instance = context.Request.Path,
            Code = code,
            Message = message,
            TraceId = context.TraceIdentifier,
            Errors = errors ?? new Dictionary<string, string[]>()
        };
    }

    public static IActionResult CreateValidationResponse(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Gia tri khong hop le."
                        : error.ErrorMessage)
                    .ToArray());

        var problem = Create(
            context.HttpContext,
            StatusCodes.Status400BadRequest,
            "validation_error",
            "Du lieu gui len khong hop le.",
            errors: errors);

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    }

    public static async Task WriteAsync(
        HttpContext context,
        int status,
        string code,
        string message,
        string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null,
        CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = status;

        var problem = Create(context, status, code, message, detail, errors);
        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
    }
}
