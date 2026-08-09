using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/notifications")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _service;
        private readonly ICurrentUserService _currentUser;

        public NotificationController(INotificationService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Lấy thông báo của user hiện tại
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<NotificationDto>>> GetMyNotifications()
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetMyNotifications(userId, HttpContext.RequestAborted));
        }

        /// <summary>
        /// Đếm thông báo chưa đọc
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetUnreadCount(userId, HttpContext.RequestAborted));
        }

        /// <summary>
        /// Đánh dấu đã đọc
        /// </summary>
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var userId = _currentUser.GetRequiredUserId();
            await _service.MarkAsRead(id, userId, HttpContext.RequestAborted);
            return NoContent();
        }
    }
}
