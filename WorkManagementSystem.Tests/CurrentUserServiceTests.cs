using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WorkManagementSystem.API.Authentication;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Domain.Common;

namespace WorkManagementSystem.Tests;

public class CurrentUserServiceTests
{
    [Fact]
    public void GetRequiredUserId_ReturnsAuthenticatedUserIdAndRole()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(AuthenticationClaimTypes.UserId, userId.ToString()),
                    new Claim(ClaimTypes.Role, SystemRoles.Manager)
                },
                "Test"))
        };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        Assert.True(service.IsAuthenticated);
        Assert.Equal(userId, service.GetRequiredUserId());
        Assert.Equal(SystemRoles.Manager, service.Role);
    }

    [Fact]
    public void GetRequiredUserId_WithoutValidClaim_ThrowsUnauthorizedException()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test"))
        };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        var exception = Assert.Throws<UnauthorizedException>(() => service.GetRequiredUserId());

        Assert.Equal(StatusCodes.Status401Unauthorized, exception.StatusCode);
        Assert.Equal("unauthorized", exception.Code);
    }
}
