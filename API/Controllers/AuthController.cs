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

        public AuthController(IAuthService service)
        {
            _service = service;
        }

        /// <summary>
        /// Đăng ký tài khoản (chờ Admin duyệt)
        /// </summary>
        [HttpPost("register")]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> Register(AuthDto dto, CancellationToken cancellationToken)
            => Ok(await _service.Register(dto, cancellationToken));

        /// <summary>
        /// Đăng nhập và lấy JWT token
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> Login(LoginDto dto, CancellationToken cancellationToken)
            => Ok(await _service.Login(dto.Username, dto.Password, cancellationToken));

        /// <summary>
        /// Đặt lại mật khẩu — CHỈ ADMIN mới được phép
        /// </summary>
        [HttpPost("reset-password")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordDto dto,
            CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var adminId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.ResetPassword(dto, adminId, cancellationToken));
        }

        /// <summary>
        /// Lấy danh sách tài khoản chờ duyệt (Admin)
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPendingUsers(CancellationToken cancellationToken)
            => Ok(await _service.GetPendingUsers(cancellationToken));

        /// <summary>
        /// Duyệt tài khoản (Admin)
        /// </summary>
        [HttpPost("approve/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveUser(Guid userId, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var adminId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.ApproveUser(userId, adminId, cancellationToken));
        }

        /// <summary>
        /// Từ chối tài khoản (Admin)
        /// </summary>
        [HttpDelete("reject/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RejectUser(Guid userId, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var adminId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.RejectUser(userId, adminId, cancellationToken));
        }
    }
}
