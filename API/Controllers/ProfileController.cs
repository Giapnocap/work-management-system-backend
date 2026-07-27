using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _service;
        private readonly IAuthService _authService;

        public ProfileController(IProfileService service, IAuthService authService)
        {
            _service = service;
            _authService = authService;
        }

        [HttpGet]
        public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var profile = await _service.GetProfile(userId, cancellationToken);
            if (profile == null)
                return NotFound(new { message = "Không tìm thấy hồ sơ.", code = "not_found" });

            return Ok(profile);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] ProfileDto dto, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var result = await _service.UpdateProfile(userId, dto, cancellationToken);

            if (result != "Cập nhật thành công!")
                return BadRequest(new { message = result, code = "business_error" });

            var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (username != null)
            {
                var newToken = await _authService.RefreshToken(userId, cancellationToken);
                return Ok(new { message = result, token = newToken });
            }

            return Ok(new { message = result });
        }
    }
}
