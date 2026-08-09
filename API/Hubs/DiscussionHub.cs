using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Hubs
{
    [Authorize]
    public class DiscussionHub : Hub
    {
        private readonly ITaskAccessService _accessService;

        public DiscussionHub(ITaskAccessService accessService)
        {
            _accessService = accessService;
        }

        public async Task JoinTaskGroup(Guid taskId)
        {
            var userId = GetCurrentUserId();
            if (!await _accessService.CanAccessTask(
                    taskId,
                    userId,
                    cancellationToken: Context.ConnectionAborted))
                throw new HubException("You do not have access to this task.");

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                TaskDiscussionGroup.For(taskId),
                Context.ConnectionAborted);
        }

        public async Task LeaveTaskGroup(Guid taskId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                TaskDiscussionGroup.For(taskId),
                Context.ConnectionAborted);
        }

        private Guid GetCurrentUserId()
        {
            var rawId = Context.User?.FindFirst(AuthenticationClaimTypes.UserId)?.Value
                ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(rawId, out var userId))
                throw new HubException("Unauthenticated.");

            return userId;
        }
    }
}
