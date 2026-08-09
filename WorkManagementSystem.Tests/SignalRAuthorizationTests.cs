using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using WorkManagementSystem.API.Hubs;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.Tests;

public class SignalRAuthorizationTests
{
    [Fact]
    public void DiscussionHub_RequiresAuthentication()
    {
        Assert.NotNull(typeof(DiscussionHub).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task JoinTaskGroup_WithTaskAccess_AddsConnectionToScopedGroup()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var groups = new RecordingGroupManager();
        var hub = CreateHub(userId, canAccess: true, groups);

        await hub.JoinTaskGroup(taskId);

        Assert.Equal(("connection-1", TaskDiscussionGroup.For(taskId)), groups.Added);
    }

    [Fact]
    public async Task JoinTaskGroup_WithoutTaskAccess_ThrowsAndDoesNotJoin()
    {
        var groups = new RecordingGroupManager();
        var hub = CreateHub(Guid.NewGuid(), canAccess: false, groups);

        await Assert.ThrowsAsync<HubException>(() => hub.JoinTaskGroup(Guid.NewGuid()));

        Assert.Null(groups.Added);
    }

    private static DiscussionHub CreateHub(
        Guid userId,
        bool canAccess,
        IGroupManager groups)
    {
        return new DiscussionHub(new StubTaskAccessService(canAccess))
        {
            Context = new TestHubCallerContext(userId),
            Groups = groups
        };
    }

    private sealed class StubTaskAccessService : ITaskAccessService
    {
        private readonly bool _canAccess;

        public StubTaskAccessService(bool canAccess)
        {
            _canAccess = canAccess;
        }

        public Task<bool> CanAccessTask(
            Guid taskId,
            Guid userId,
            bool managementOnly = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_canAccess);

        public Task<bool> CanManageUnit(
            Guid unitId,
            Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> CanAccessUpload(
            Guid uploadId,
            Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<string?> GetUserRole(
            Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public (string ConnectionId, string GroupName)? Added { get; private set; }

        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            Added = (connectionId, groupName);
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestHubCallerContext : HubCallerContext
    {
        public TestHubCallerContext(Guid userId)
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(AuthenticationClaimTypes.UserId, userId.ToString())
            }, "Test"));
        }

        public override string ConnectionId => "connection-1";
        public override string? UserIdentifier => User?.FindFirst(AuthenticationClaimTypes.UserId)?.Value;
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort()
        {
        }
    }
}
