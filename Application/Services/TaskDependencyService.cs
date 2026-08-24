using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services;

public sealed class TaskDependencyService : ITaskDependencyService
{
    private readonly IAppDbContext _context;
    private readonly ITaskAccessService _accessService;
    private readonly ITransactionManager _transactionManager;

    public TaskDependencyService(
        IAppDbContext context,
        ITaskAccessService accessService,
        ITransactionManager transactionManager)
    {
        _context = context;
        _accessService = accessService;
        _transactionManager = transactionManager;
    }

    public Task<TaskDependencyDto> AddAsync(
        Guid taskId,
        Guid dependsOnTaskId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
        => _transactionManager.ExecuteSerializableAsync(
            token => AddCoreAsync(taskId, dependsOnTaskId, actorUserId, token),
            cancellationToken);

    public async Task RemoveAsync(
        Guid taskId,
        Guid dependsOnTaskId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await _transactionManager.ExecuteSerializableAsync(async token =>
        {
            await RemoveCoreAsync(taskId, dependsOnTaskId, actorUserId, token);
            return true;
        }, cancellationToken);
    }

    public async Task<List<TaskDependencyDto>> GetAsync(
        Guid taskId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        await GetAccessibleTaskAsync(taskId, requesterId, cancellationToken);

        var dependencies = await (
                from dependency in _context.TaskDependencies.AsNoTracking()
                join predecessor in _context.Tasks.AsNoTracking()
                    on dependency.DependsOnTaskId equals predecessor.Id
                where dependency.TaskId == taskId
                orderby predecessor.Title, predecessor.Id
                select new
                {
                    dependency.Id,
                    dependency.TaskId,
                    dependency.DependsOnTaskId,
                    predecessor.Title,
                    predecessor.Status,
                    dependency.CreatedAt,
                    dependency.CreatedByUserId
                })
            .ToListAsync(cancellationToken);

        return dependencies.Select(dependency => new TaskDependencyDto
        {
            Id = dependency.Id,
            TaskId = dependency.TaskId,
            DependsOnTaskId = dependency.DependsOnTaskId,
            DependsOnTaskTitle = dependency.Title,
            DependsOnTaskStatus = dependency.Status.ToString(),
            CreatedAt = dependency.CreatedAt,
            CreatedByUserId = dependency.CreatedByUserId
        }).ToList();
    }

    public async Task<TaskDependencyGraphDto> GetGraphAsync(
        Guid taskId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        var rootTask = await GetAccessibleTaskAsync(taskId, requesterId, cancellationToken);
        var scopedTasks = BuildScopeQuery(rootTask);

        var nodes = await scopedTasks
            .Select(task => new GraphNode(task.Id, task.Title, task.Status))
            .ToListAsync(cancellationToken);

        var scopedTaskIds = scopedTasks.Select(task => task.Id);
        var edges = await _context.TaskDependencies
            .AsNoTracking()
            .Where(dependency =>
                scopedTaskIds.Contains(dependency.TaskId) &&
                scopedTaskIds.Contains(dependency.DependsOnTaskId))
            .Select(dependency => new DependencyEdge(
                dependency.TaskId,
                dependency.DependsOnTaskId))
            .ToListAsync(cancellationToken);

        var reachableTaskIds = FindReachableTaskIds(taskId, edges);
        var reachableEdges = edges
            .Where(edge => reachableTaskIds.Contains(edge.TaskId))
            .OrderBy(edge => edge.TaskId)
            .ThenBy(edge => edge.DependsOnTaskId)
            .ToList();
        var nodeMap = nodes.ToDictionary(node => node.Id);
        var edgesByTask = reachableEdges.ToLookup(edge => edge.TaskId);

        return new TaskDependencyGraphDto
        {
            RootTaskId = taskId,
            Nodes = reachableTaskIds
                .Where(nodeMap.ContainsKey)
                .Select(id => nodeMap[id])
                .OrderBy(node => node.Title)
                .ThenBy(node => node.Id)
                .Select(node => new TaskDependencyNodeDto
                {
                    Id = node.Id,
                    Title = node.Title,
                    Status = node.Status.ToString(),
                    IsBlocked = edgesByTask[node.Id]
                        .Any(edge => nodeMap.TryGetValue(edge.DependsOnTaskId, out var predecessor) &&
                                     predecessor.Status != TaskStatusEnum.Approved)
                })
                .ToList(),
            Edges = reachableEdges.Select(edge => new TaskDependencyEdgeDto
            {
                TaskId = edge.TaskId,
                DependsOnTaskId = edge.DependsOnTaskId
            }).ToList()
        };
    }

    private async Task<TaskDependencyDto> AddCoreAsync(
        Guid taskId,
        Guid dependsOnTaskId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty || dependsOnTaskId == Guid.Empty)
            throw new BusinessException("Công việc và công việc tiên quyết phải hợp lệ.");

        if (taskId == dependsOnTaskId)
            throw new BusinessException("Công việc không thể phụ thuộc chính nó.");

        var task = await GetTaskAsync(taskId, cancellationToken);
        await EnsureCanManageAsync(task.Id, actorUserId, cancellationToken);

        var predecessor = await GetTaskAsync(dependsOnTaskId, cancellationToken);
        await EnsureCanManageAsync(predecessor.Id, actorUserId, cancellationToken);

        EnsureDependencyCanChange(task);
        EnsureSameScope(task, predecessor);

        if (await _context.TaskDependencies.AnyAsync(
                dependency => dependency.TaskId == taskId &&
                              dependency.DependsOnTaskId == dependsOnTaskId,
                cancellationToken))
        {
            throw new BusinessException("Quan hệ phụ thuộc này đã tồn tại.");
        }

        var existingEdges = await BuildScopeQuery(task)
            .Select(scopeTask => scopeTask.Id)
            .Join(
                _context.TaskDependencies.AsNoTracking(),
                id => id,
                dependency => dependency.TaskId,
                (_, dependency) => new DependencyEdge(
                    dependency.TaskId,
                    dependency.DependsOnTaskId))
            .ToListAsync(cancellationToken);

        if (WouldCreateCycle(taskId, dependsOnTaskId, existingEdges))
            throw new BusinessException("Quan hệ phụ thuộc tạo thành chu trình công việc không hợp lệ.");

        var dependency = new TaskDependency
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            DependsOnTaskId = dependsOnTaskId,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = actorUserId
        };

        await _context.TaskDependencies.AddAsync(dependency, cancellationToken);
        await AddHistoryAsync(
            taskId,
            actorUserId,
            "DependencyAdded",
            null,
            FormatDependency(predecessor),
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new TaskDependencyDto
        {
            Id = dependency.Id,
            TaskId = dependency.TaskId,
            DependsOnTaskId = dependency.DependsOnTaskId,
            DependsOnTaskTitle = predecessor.Title,
            DependsOnTaskStatus = predecessor.Status.ToString(),
            CreatedAt = dependency.CreatedAt,
            CreatedByUserId = dependency.CreatedByUserId
        };
    }

    private async Task RemoveCoreAsync(
        Guid taskId,
        Guid dependsOnTaskId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await EnsureCanManageAsync(task.Id, actorUserId, cancellationToken);

        var predecessor = await GetTaskAsync(dependsOnTaskId, cancellationToken);
        await EnsureCanManageAsync(predecessor.Id, actorUserId, cancellationToken);

        EnsureDependencyCanChange(task);
        EnsureSameScope(task, predecessor);

        var dependency = await _context.TaskDependencies.SingleOrDefaultAsync(
            item => item.TaskId == taskId && item.DependsOnTaskId == dependsOnTaskId,
            cancellationToken);
        if (dependency == null)
            throw new NotFoundException("Không tìm thấy quan hệ phụ thuộc.");

        var wasBlocking = predecessor.Status != TaskStatusEnum.Approved;
        _context.TaskDependencies.Remove(dependency);
        await AddHistoryAsync(
            taskId,
            actorUserId,
            "DependencyRemoved",
            FormatDependency(predecessor),
            null,
            cancellationToken);

        if (wasBlocking && !await HasOtherBlockingDependencyAsync(
                taskId,
                dependency.Id,
                cancellationToken))
        {
            await AddHistoryAsync(
                taskId,
                actorUserId,
                "DependencyUnblocked",
                bool.TrueString,
                bool.FalseString,
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<TaskItem> GetAccessibleTaskAsync(
        Guid taskId,
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        if (!await _accessService.CanAccessTask(
                taskId,
                requesterId,
                cancellationToken: cancellationToken))
        {
            throw new ForbiddenException("Bạn không có quyền xem quan hệ phụ thuộc của công việc này.");
        }

        return task;
    }

    private async Task<TaskItem> GetTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        return await _context.Tasks
            .AsNoTracking()
            .SingleOrDefaultAsync(task => task.Id == taskId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy công việc.");
    }

    private async Task EnsureCanManageAsync(
        Guid taskId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var role = await _accessService.GetUserRole(actorUserId, cancellationToken);
        if (role != SystemRoles.Manager ||
            !await _accessService.CanAccessTask(
                taskId,
                actorUserId,
                managementOnly: true,
                cancellationToken))
        {
            throw new ForbiddenException("Bạn không có quyền quản lý quan hệ phụ thuộc của công việc này.");
        }
    }

    private IQueryable<TaskItem> BuildScopeQuery(TaskItem task)
    {
        var query = _context.Tasks
            .AsNoTracking()
            .Where(candidate => candidate.UnitId == task.UnitId);

        return task.ProjectId.HasValue
            ? query.Where(candidate => candidate.ProjectId == task.ProjectId.Value)
            : query.Where(candidate => candidate.ProjectId == null);
    }

    private static void EnsureDependencyCanChange(TaskItem task)
    {
        if (task.Status == TaskStatusEnum.Approved)
            throw new BusinessException("Công việc đã hoàn thành, không thể thay đổi quan hệ phụ thuộc.");
    }

    private static void EnsureSameScope(TaskItem task, TaskItem predecessor)
    {
        if (task.UnitId != predecessor.UnitId)
            throw new ForbiddenException("Hai công việc phải thuộc cùng một phòng ban.");

        if (task.ProjectId != predecessor.ProjectId)
            throw new BusinessException("Hai công việc phải thuộc cùng một dự án.");
    }

    private async Task<bool> HasOtherBlockingDependencyAsync(
        Guid taskId,
        Guid removedDependencyId,
        CancellationToken cancellationToken)
    {
        return await (
                from dependency in _context.TaskDependencies.AsNoTracking()
                join predecessor in _context.Tasks.AsNoTracking()
                    on dependency.DependsOnTaskId equals predecessor.Id
                where dependency.TaskId == taskId &&
                      dependency.Id != removedDependencyId &&
                      predecessor.Status != TaskStatusEnum.Approved
                select dependency.Id)
            .AnyAsync(cancellationToken);
    }

    private async Task AddHistoryAsync(
        Guid taskId,
        Guid changedBy,
        string fieldName,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken)
    {
        await _context.TaskHistories.AddAsync(new TaskHistory
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            ChangedBy = changedBy,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    private static string FormatDependency(TaskItem predecessor)
        => $"{predecessor.Id}:{predecessor.Title}";

    private static bool WouldCreateCycle(
        Guid taskId,
        Guid dependsOnTaskId,
        IReadOnlyCollection<DependencyEdge> existingEdges)
    {
        var dependenciesByTask = existingEdges.ToLookup(edge => edge.TaskId);
        var pending = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        pending.Push(dependsOnTaskId);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current == taskId)
                return true;

            if (!visited.Add(current))
                continue;

            foreach (var edge in dependenciesByTask[current])
                pending.Push(edge.DependsOnTaskId);
        }

        return false;
    }

    private static HashSet<Guid> FindReachableTaskIds(
        Guid rootTaskId,
        IReadOnlyCollection<DependencyEdge> edges)
    {
        var dependenciesByTask = edges.ToLookup(edge => edge.TaskId);
        var reachable = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(rootTaskId);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!reachable.Add(current))
                continue;

            foreach (var edge in dependenciesByTask[current])
                pending.Push(edge.DependsOnTaskId);
        }

        return reachable;
    }

    private sealed record GraphNode(Guid Id, string Title, TaskStatusEnum Status);
    private sealed record DependencyEdge(Guid TaskId, Guid DependsOnTaskId);
}
