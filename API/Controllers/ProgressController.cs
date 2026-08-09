using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/progress")]
    public class ProgressController : ControllerBase
    {
        private readonly IProgressService _service;
        private readonly IProgressQueryService _queryService;
        private readonly ICurrentUserService _currentUser;

        public ProgressController(
            IProgressService service,
            IProgressQueryService queryService,
            ICurrentUserService currentUser)
        {
            _service = service;
            _queryService = queryService;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<ProgressDto>>> GetAll(
            int page = 1,
            int size = 10,
            bool myProgress = false)
        {
            var currentUserId = _currentUser.GetRequiredUserId();
            return Ok(await _queryService.GetAll(page, size, currentUserId, myProgress, HttpContext.RequestAborted));
        }

        [HttpPost]
        public async Task<ActionResult<ProgressDto>> Update(CreateProgressDto dto)
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.Update(dto, userId, HttpContext.RequestAborted));
        }

        [HttpGet("task/{taskId}")]
        public async Task<ActionResult<List<ProgressDto>>> GetByTask(Guid taskId)
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _queryService.GetByTaskAsync(taskId, userId, HttpContext.RequestAborted));
        }

        [HttpGet("my-history")]
        public async Task<ActionResult<PagedResult<ProgressDto>>> GetMyHistory(int page = 1, int size = 20)
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _queryService.GetMyHistory(userId, page, size, HttpContext.RequestAborted));
        }
    }
}
