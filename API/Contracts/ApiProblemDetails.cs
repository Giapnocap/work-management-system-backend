using Microsoft.AspNetCore.Mvc;

namespace WorkManagementSystem.API.Contracts;

public sealed class ApiProblemDetails : ProblemDetails
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string[]> Errors { get; init; }
        = new Dictionary<string, string[]>();
}
