using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _service;
        private readonly ICurrentUserService _currentUser;

        public AuthController(IAuthService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        [HttpPost("register")]
        [EnableRateLimiting("authentication")]
        public async Task<ActionResult<string>> Register(AuthDto dto, CancellationToken cancellationToken)
        {
            var message = await _service.Register(dto, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, message);
        }

        [HttpPost("login")]
        [EnableRateLimiting("authentication")]
        public async Task<ActionResult<string>> Login(LoginDto dto, CancellationToken cancellationToken)
            => Ok(await _service.Login(dto.Username, dto.Password, cancellationToken));

        [HttpPost("reset-password")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<ActionResult<string>> ResetPassword(
            ResetPasswordDto dto,
            CancellationToken cancellationToken)
        {
            var adminId = _currentUser.GetRequiredUserId();
            return Ok(await _service.ResetPassword(dto, adminId, cancellationToken));
        }

        [HttpGet("pending")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<ActionResult<List<UserDto>>> GetPendingUsers(CancellationToken cancellationToken)
            => Ok(await _service.GetPendingUsers(cancellationToken));

        [HttpPost("approve/{userId}")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<ActionResult<string>> ApproveUser(Guid userId, CancellationToken cancellationToken)
        {
            var adminId = _currentUser.GetRequiredUserId();
            return Ok(await _service.ApproveUser(userId, adminId, cancellationToken));
        }

        [HttpDelete("reject/{userId}")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<IActionResult> RejectUser(Guid userId, CancellationToken cancellationToken)
        {
            var adminId = _currentUser.GetRequiredUserId();
            await _service.RejectUser(userId, adminId, cancellationToken);
            return NoContent();
        }
    }
}
