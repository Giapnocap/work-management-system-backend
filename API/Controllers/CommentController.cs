using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WorkManagementSystem.API.Hubs;
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
        private readonly IHubContext<DiscussionHub> _hubContext;

        public CommentController(ICommentService service, IHubContext<DiscussionHub> hubContext)
        {
            _service = service;
            _hubContext = hubContext;
        }

        [HttpPost]
        public async Task<IActionResult> Add(CreateCommentDto dto, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var result = await _service.AddComment(dto, userId, cancellationToken);
            await _hubContext.Clients.Group(dto.TaskId.ToString())
                .SendAsync("ReceiveComment", result, cancellationToken);

            return Ok(result);
        }

        [HttpGet("{taskId}")]
        public async Task<IActionResult> GetByTaskId(Guid taskId, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.GetComments(taskId, userId, cancellationToken));
        }

        [HttpPost("{id}/react")]
        public async Task<IActionResult> React(Guid id, [FromBody] string emoji, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var taskId = await _service.ToggleReaction(id, userId, emoji, cancellationToken);
            await _hubContext.Clients.Group(taskId.ToString())
                .SendAsync("UpdateReaction", id, cancellationToken);

            return Ok();
        }

        [HttpPost("task/{taskId}/seen")]
        public async Task<IActionResult> MarkSeen(Guid taskId, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            await _service.MarkAsSeen(taskId, userId, cancellationToken);
            await _hubContext.Clients.Group(taskId.ToString())
                .SendAsync("UpdateSeen", userId, cancellationToken);

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
    }
}
