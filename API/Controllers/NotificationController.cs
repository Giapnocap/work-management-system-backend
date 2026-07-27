using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/notifications")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _service;

        public NotificationController(INotificationService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lấy thông báo của user hiện tại
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.GetMyNotifications(userId, HttpContext.RequestAborted));
        }

        /// <summary>
        /// Đếm thông báo chưa đọc
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.GetUnreadCount(userId, HttpContext.RequestAborted));
        }

        /// <summary>
        /// Đánh dấu đã đọc
        /// </summary>
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            await _service.MarkAsRead(id, userId, HttpContext.RequestAborted);
            return Ok();
        }
    }
}
