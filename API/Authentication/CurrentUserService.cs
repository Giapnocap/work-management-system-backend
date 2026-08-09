using System.Security.Claims;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Authentication;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public string? Role => User?.FindFirst(ClaimTypes.Role)?.Value
        ?? User?.FindFirst("role")?.Value;

    public Guid GetRequiredUserId()
    {
        var value = User?.FindFirst(AuthenticationClaimTypes.UserId)?.Value
            ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedException();
    }
}
