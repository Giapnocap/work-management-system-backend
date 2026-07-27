using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/progress")]
    public class ProgressController : ControllerBase
    {
        private readonly IProgressService _service;
        private readonly IGenericRepository<User> _userRepo;

        public ProgressController(
            IProgressService service,
            IGenericRepository<User> userRepo)
        {
            _service = service;
            _userRepo = userRepo;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            int page = 1,
            int size = 10,
            bool myProgress = false)
        {
            var role = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value)?.Trim();
            if (!User.TryGetUserId(out var currentUserId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            Guid? userId = null;
            Guid? unitId = null;

            if (myProgress || string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
                userId = currentUserId;

            if (role != null && role.Equals("Manager", StringComparison.OrdinalIgnoreCase) && !myProgress)
            {
                var manager = await _userRepo.GetByIdAsync(currentUserId, HttpContext.RequestAborted);
                unitId = manager?.UnitId;
            }

            return Ok(await _service.GetAll(page, size, userId, unitId, HttpContext.RequestAborted));
        }

        [HttpPost]
        public async Task<IActionResult> Update(CreateProgressDto dto)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.Update(dto, userId, HttpContext.RequestAborted));
        }

        [HttpGet("task/{taskId}")]
        public async Task<IActionResult> GetByTask(Guid taskId)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.GetByTaskAsync(taskId, userId, HttpContext.RequestAborted));
        }

        [HttpGet("my-history")]
        public async Task<IActionResult> GetMyHistory(int page = 1, int size = 20)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.GetMyHistory(userId, page, size, HttpContext.RequestAborted));
        }
    }
}
