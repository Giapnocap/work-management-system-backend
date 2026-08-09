using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/change-password")]
    public class ChangePasswordController : ControllerBase
    {
        private readonly IChangePasswordService _service;
        private readonly ICurrentUserService _currentUser;

        public ChangePasswordController(IChangePasswordService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Đổi mật khẩu
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto, CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            await _service.ChangePassword(userId, dto, cancellationToken);
            return NoContent();
        }
    }
}
