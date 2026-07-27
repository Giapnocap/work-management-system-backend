using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize(Roles = "Manager")]
    [ApiController]
    [Route("api/review")]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService _service;

        public ReviewController(IReviewService service)
        {
            _service = service;
        }

        /// <summary>
        /// Phê duyệt hoặc từ chối báo cáo (Manager)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Review(ReviewDto dto)
        {
            if (!User.TryGetUserId(out var reviewerId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.Review(dto, reviewerId, HttpContext.RequestAborted));
        }
    }
}
