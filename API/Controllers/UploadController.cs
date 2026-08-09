using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly IUploadService _uploadService;
        private readonly ICurrentUserService _currentUser;

        public UploadController(IUploadService uploadService, ICurrentUserService currentUser)
        {
            _uploadService = uploadService;
            _currentUser = currentUser;
        }

        [Authorize]
        [HttpPost]
        [EnableRateLimiting("uploads")]
        public async Task<ActionResult<UploadFileDto>> Upload(
            IFormFile file,
            Guid? progressId,
            Guid? taskId)
        {
            var userId = _currentUser.GetRequiredUserId();
            var result = await _uploadService.UploadAsync(
                file,
                progressId,
                taskId,
                userId,
                HttpContext.RequestAborted);
            return CreatedAtAction(nameof(Download), new { id = result.Id }, result);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var userId = _currentUser.GetRequiredUserId();
            var file = await _uploadService.GetFileForDownloadAsync(
                id,
                userId,
                HttpContext.RequestAborted);

            Response.Headers["X-Content-Type-Options"] = "nosniff";

            return PhysicalFile(
                file.PhysicalPath,
                file.ContentType,
                file.FileName,
                enableRangeProcessing: true);
        }
    }
}
