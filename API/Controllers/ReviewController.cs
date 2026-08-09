using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize(Roles = SystemRoles.Manager)]
    [ApiController]
    [Route("api/review")]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService _service;
        private readonly ICurrentUserService _currentUser;

        public ReviewController(IReviewService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Phê duyệt hoặc từ chối báo cáo (Manager)
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ReviewDto>> Review(ReviewDto dto)
        {
            var reviewerId = _currentUser.GetRequiredUserId();
            return Ok(await _service.Review(dto, reviewerId, HttpContext.RequestAborted));
        }
    }
}
