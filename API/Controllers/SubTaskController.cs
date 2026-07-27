using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/subtasks")]
    public class SubTaskController : ControllerBase
    {
        private readonly ISubTaskService _service;

        public SubTaskController(ISubTaskService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateSubTaskDto dto, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var result = await _service.AddSubTask(dto, userId, cancellationToken);
            return Ok(result);
        }

        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> Toggle(Guid id, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            await _service.ToggleSubTask(id, userId, cancellationToken);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            await _service.Delete(id, userId, cancellationToken);
            return Ok();
        }

        [HttpGet("task/{taskId}")]
        public async Task<IActionResult> GetByTask(Guid taskId, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var result = await _service.GetSubTasks(taskId, userId, cancellationToken);
            return Ok(result);
        }
    }
}
