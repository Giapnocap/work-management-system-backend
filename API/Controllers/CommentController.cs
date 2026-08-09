using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/comments")]
    public class CommentController : ControllerBase
    {
        private readonly ICommentService _service;
        private readonly ICurrentUserService _currentUser;

        public CommentController(
            ICommentService service,
            ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        [HttpPost]
        public async Task<ActionResult<CommentDto>> Add(CreateCommentDto dto, CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            var result = await _service.AddComment(dto, userId, cancellationToken);
            return CreatedAtAction(nameof(GetByTaskId), new { taskId = dto.TaskId }, result);
        }

        [HttpGet("{taskId}")]
        public async Task<ActionResult<List<CommentDto>>> GetByTaskId(
            Guid taskId,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetComments(taskId, userId, cancellationToken));
        }

        [HttpPost("{id}/react")]
        public async Task<IActionResult> React(Guid id, [FromBody] string emoji, CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            await _service.ToggleReaction(id, userId, emoji, cancellationToken);

            return NoContent();
        }

        [HttpPost("task/{taskId}/seen")]
        public async Task<IActionResult> MarkSeen(Guid taskId, CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            await _service.MarkAsSeen(taskId, userId, cancellationToken);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            await _service.Delete(id, userId, cancellationToken);
            return NoContent();
        }
    }
}
