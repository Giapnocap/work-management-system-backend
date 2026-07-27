using System.Security.Claims;
using WorkManagementSystem.Application.Common;

namespace WorkManagementSystem.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
        {
            var idClaim = principal.FindFirst(AuthenticationClaimTypes.UserId)?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(idClaim, out userId);
        }
    }
}
