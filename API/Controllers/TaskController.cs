using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.Common;
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
        private readonly ITaskQueryService _queryService;
        private readonly ICurrentUserService _currentUser;

        public TaskController(
            ITaskService service,
            ITaskQueryService queryService,
            ICurrentUserService currentUser)
        {
            _service = service;
            _queryService = queryService;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<TaskDto>>> Get(
            string? keyword,
            string? status,
            Guid? projectId,
            int page = 1,
            int size = 10,
            bool myTasks = false)
        {
            var currentUserId = _currentUser.GetRequiredUserId();
            var result = await _queryService.Get(
                keyword ?? "", page, size, status, currentUserId, myTasks, projectId,
                HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = SystemRoles.Manager)]
        public async Task<ActionResult<TaskDto>> Create(CreateTaskDto dto)
        {
            var userId = _currentUser.GetRequiredUserId();
            var result = await _service.Create(dto, userId, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = SystemRoles.Manager)]
        public async Task<ActionResult<TaskDto>> Update(Guid id, UpdateTaskDto dto)
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.Update(id, dto, userId, HttpContext.RequestAborted));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = SystemRoles.Manager)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = _currentUser.GetRequiredUserId();
            await _service.Delete(id, userId, HttpContext.RequestAborted);
            return NoContent();
        }

        [HttpPost("{id}/remind")]
        [Authorize(Roles = SystemRoles.Manager)]
        public async Task<IActionResult> Remind(Guid id)
        {
            var managerId = _currentUser.GetRequiredUserId();
            await _service.RemindTask(id, managerId, HttpContext.RequestAborted);
            return NoContent();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TaskDto>> GetById(Guid id)
        {
            var userId = _currentUser.GetRequiredUserId();
            var result = await _queryService.GetByIdAsync(id, userId, HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpGet("{id}/history")]
        public async Task<ActionResult<List<TaskHistoryDto>>> GetHistory(Guid id)
        {
            var userId = _currentUser.GetRequiredUserId();
            var result = await _queryService.GetHistoryAsync(id, userId, HttpContext.RequestAborted);
            return Ok(result);
        }
    }
}
