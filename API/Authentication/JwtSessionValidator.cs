using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Authentication
{
    public static class JwtSessionValidator
    {
        public static async Task ValidateAsync(TokenValidatedContext context)
        {
            var userIdValue = context.Principal?
                .FindFirst(AuthenticationClaimTypes.UserId)?.Value;
            var tokenVersionValue = context.Principal?
                .FindFirst(AuthenticationClaimTypes.TokenVersion)?.Value;
            var role = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;

            if (!Guid.TryParse(userIdValue, out var userId) ||
                !int.TryParse(tokenVersionValue, out var tokenVersion) ||
                string.IsNullOrWhiteSpace(role))
            {
                context.Fail("Token does not contain a valid session identity.");
                return;
            }

            var dbContext = context.HttpContext.RequestServices
                .GetRequiredService<IAppDbContext>();

            var account = await dbContext.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    user.IsApproved,
                    user.IsDeleted,
                    user.Role,
                    user.TokenVersion
                })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

            if (account == null ||
                !account.IsApproved ||
                account.IsDeleted ||
                account.Role != role ||
                account.TokenVersion != tokenVersion)
            {
                context.Fail("Token session is no longer valid.");
            }
        }
    }
}
