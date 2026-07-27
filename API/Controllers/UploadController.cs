using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly IUploadService _uploadService;

        public UploadController(IUploadService uploadService)
        {
            _uploadService = uploadService;
        }

        [Authorize]
        [HttpPost]
        [EnableRateLimiting("uploads")]
        public async Task<IActionResult> Upload(IFormFile file, Guid? progressId, Guid? taskId)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var result = await _uploadService.UploadAsync(
                file,
                progressId,
                taskId,
                userId,
                HttpContext.RequestAborted);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var file = await _uploadService.GetFileForDownloadAsync(
                id,
                userId,
                HttpContext.RequestAborted);
            if (file == null) return NotFound(new { message = "File not found.", code = "not_found" });

            if (!System.IO.File.Exists(file.FilePath))
                return NotFound(new { message = "File physical content not found.", code = "not_found" });

            Response.Headers["X-Content-Type-Options"] = "nosniff";

            return PhysicalFile(
                file.FilePath,
                file.ContentType,
                file.FileName,
                enableRangeProcessing: true);
        }
    }
}
