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
        private readonly ICurrentUserService _currentUser;

        public SubTaskController(ISubTaskService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        [HttpPost]
        public async Task<ActionResult<SubTaskDto>> Create(
            CreateSubTaskDto dto,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            var result = await _service.AddSubTask(dto, userId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> Toggle(Guid id, CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            await _service.ToggleSubTask(id, userId, cancellationToken);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            await _service.Delete(id, userId, cancellationToken);
            return NoContent();
        }

        [HttpGet("task/{taskId}")]
        public async Task<ActionResult<List<SubTaskDto>>> GetByTask(
            Guid taskId,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            var result = await _service.GetSubTasks(taskId, userId, cancellationToken);
            return Ok(result);
        }
    }
}
