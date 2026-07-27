using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/tasks")]
    public class TaskController : ControllerBase
    {
        private readonly ITaskService _service;

        public TaskController(ITaskService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            string? keyword,
            string? status,
            Guid? projectId,
            int page = 1,
            int size = 10,
            bool myTasks = false)
        {
            if (!User.TryGetUserId(out var currentUserId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var role = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value)?.Trim()?.ToLower();
            Guid? userId = myTasks ? currentUserId : null;
            Guid? managerUnitId = role == "manager"
                ? await _service.GetManagerUnitId(currentUserId, HttpContext.RequestAborted)
                : null;

            var result = await _service.Get(
                keyword ?? "", page, size, status, currentUserId, userId, managerUnitId, projectId,
                HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Create(CreateTaskDto dto)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var result = await _service.Create(dto, userId, HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Update(Guid id, UpdateTaskDto dto)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.Update(id, dto, userId, HttpContext.RequestAborted));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            await _service.Delete(id, userId, HttpContext.RequestAborted);
            return Ok(new { message = "Deleted successfully" });
        }

        [HttpPost("{id}/remind")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Remind(Guid id)
        {
            if (!User.TryGetUserId(out var managerId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            await _service.RemindTask(id, managerId, HttpContext.RequestAborted);
            return Ok(new { message = "Reminder sent successfully" });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var result = await _service.GetByIdAsync(id, userId, HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetHistory(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var result = await _service.GetHistoryAsync(id, userId, HttpContext.RequestAborted);
            return Ok(result);
        }
    }
}
