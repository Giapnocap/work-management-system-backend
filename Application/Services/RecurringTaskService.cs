using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Application.Services;

public sealed class RecurringTaskService : IRecurringTaskService
{
    private readonly IAppDbContext _context;
    private readonly ITaskBusinessRuleService _taskRules;
    private readonly IRecurringScheduleCalculator _scheduleCalculator;
    private readonly ITransactionManager _transactionManager;
    private readonly IAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    public RecurringTaskService(
        IAppDbContext context,
        ITaskBusinessRuleService taskRules,
        IRecurringScheduleCalculator scheduleCalculator,
        ITransactionManager transactionManager,
        IAuditService auditService,
        TimeProvider timeProvider)
    {
        _context = context;
        _taskRules = taskRules;
        _scheduleCalculator = scheduleCalculator;
        _transactionManager = transactionManager;
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    public async Task<List<RecurringTaskTemplateDto>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var manager = await GetManagerAsync(userId, cancellationToken);
        var templates = await TemplateQuery()
            .Where(template => template.UnitId == manager.UnitId)
            .OrderBy(template => template.NextRunAtUtc)
            .ThenBy(template => template.Title)
            .ToListAsync(cancellationToken);

        return templates.Select(Map).ToList();
    }

    public async Task<RecurringTaskTemplateDto> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var manager = await GetManagerAsync(userId, cancellationToken);
        var template = await TemplateQuery()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id && candidate.UnitId == manager.UnitId,
                cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lịch công việc định kỳ.");

        return Map(template);
    }

    public Task<RecurringTaskTemplateDto> CreateAsync(
        CreateRecurringTaskDto dto,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _transactionManager.ExecuteSerializableAsync(
            token => CreateCoreAsync(dto, userId, token),
            cancellationToken);

    public Task<RecurringTaskTemplateDto> UpdateAsync(
        Guid id,
        UpdateRecurringTaskDto dto,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _transactionManager.ExecuteSerializableAsync(
            token => UpdateCoreAsync(id, dto, userId, token),
            cancellationToken);

    public Task<RecurringTaskTemplateDto> PauseAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _transactionManager.ExecuteSerializableAsync(
            token => SetActiveAsync(id, userId, isActive: false, token),
            cancellationToken);

    public Task<RecurringTaskTemplateDto> ResumeAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _transactionManager.ExecuteSerializableAsync(
            token => SetActiveAsync(id, userId, isActive: true, token),
            cancellationToken);

    public Task DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _transactionManager.ExecuteSerializableAsync(
            async token =>
            {
                var manager = await GetManagerAsync(userId, token);
                var template = await GetManagedTemplateAsync(id, manager.UnitId!.Value, token);

                template.IsActive = false;
                template.IsDeleted = true;
                await _auditService.RecordAsync(
                    AuditEntityTypes.RecurringTask,
                    template.Id,
                    AuditActions.Deleted,
                    userId,
                    new { template.Title, template.UnitId },
                    token);
                await _context.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);

    private async Task<RecurringTaskTemplateDto> CreateCoreAsync(
        CreateRecurringTaskDto dto,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var manager = await GetManagerAsync(userId, cancellationToken);
        var unitId = manager.UnitId!.Value;
        var definition = await ValidateDefinitionAsync(dto, unitId, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var template = new RecurringTaskTemplate
        {
            Id = Guid.NewGuid(),
            UnitId = unitId,
            ProjectId = dto.ProjectId,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            Priority = definition.Priority,
            RequiresReview = dto.RequiresReview,
            PlannedEffortHours = dto.PlannedEffortHours,
            RecurrenceType = definition.RecurrenceType,
            Interval = dto.Interval,
            DayOfWeek = dto.DayOfWeek,
            DayOfMonth = dto.DayOfMonth,
            NextRunAtUtc = definition.NextRunAtUtc,
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAtUtc = now
        };

        _context.RecurringTaskTemplates.Add(template);
        AddAssignees(template.Id, definition.AssigneeUserIds);
        await _auditService.RecordAsync(
            AuditEntityTypes.RecurringTask,
            template.Id,
            AuditActions.Created,
            userId,
            new
            {
                template.Title,
                template.UnitId,
                template.ProjectId,
                RecurrenceType = template.RecurrenceType.ToString(),
                template.NextRunAtUtc
            },
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await LoadDtoAsync(template.Id, unitId, cancellationToken);
    }

    private async Task<RecurringTaskTemplateDto> UpdateCoreAsync(
        Guid id,
        UpdateRecurringTaskDto dto,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var manager = await GetManagerAsync(userId, cancellationToken);
        var unitId = manager.UnitId!.Value;
        var template = await _context.RecurringTaskTemplates
            .Include(candidate => candidate.Assignees)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id && candidate.UnitId == unitId,
                cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lịch công việc định kỳ.");
        var definition = await ValidateDefinitionAsync(dto, unitId, cancellationToken);

        var occurrenceAlreadyExists = await _context.GeneratedTaskOccurrences
            .AsNoTracking()
            .AnyAsync(
                occurrence => occurrence.TemplateId == id &&
                              occurrence.ScheduledForUtc == definition.NextRunAtUtc,
                cancellationToken);
        if (occurrenceAlreadyExists)
            throw new BusinessException("Thời điểm chạy tiếp theo đã được sinh công việc trước đó.");

        _context.SetOriginalRowVersion(template, ConcurrencyToken.Require(dto.RowVersion));
        template.ProjectId = dto.ProjectId;
        template.Title = dto.Title.Trim();
        template.Description = dto.Description?.Trim() ?? string.Empty;
        template.Priority = definition.Priority;
        template.RequiresReview = dto.RequiresReview;
        template.PlannedEffortHours = dto.PlannedEffortHours;
        template.RecurrenceType = definition.RecurrenceType;
        template.Interval = dto.Interval;
        template.DayOfWeek = dto.DayOfWeek;
        template.DayOfMonth = dto.DayOfMonth;
        template.NextRunAtUtc = definition.NextRunAtUtc;

        ReplaceAssignees(template, definition.AssigneeUserIds);
        await _auditService.RecordAsync(
            AuditEntityTypes.RecurringTask,
            template.Id,
            AuditActions.Updated,
            userId,
            new
            {
                template.Title,
                template.ProjectId,
                RecurrenceType = template.RecurrenceType.ToString(),
                template.NextRunAtUtc
            },
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await LoadDtoAsync(template.Id, unitId, cancellationToken);
    }

    private async Task<RecurringTaskTemplateDto> SetActiveAsync(
        Guid id,
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var manager = await GetManagerAsync(userId, cancellationToken);
        var unitId = manager.UnitId!.Value;
        var template = await GetManagedTemplateAsync(id, unitId, cancellationToken);

        if (isActive)
        {
            _scheduleCalculator.Validate(
                template.RecurrenceType,
                template.Interval,
                template.DayOfWeek,
                template.DayOfMonth,
                template.NextRunAtUtc);
            await EnsureProjectIsAvailableAsync(template.ProjectId, unitId, cancellationToken);
            await ValidateExplicitAssigneesAsync(
                template.Assignees.Select(assignee => assignee.UserId),
                unitId,
                cancellationToken);
        }

        if (template.IsActive != isActive)
        {
            template.IsActive = isActive;
            await _auditService.RecordAsync(
                AuditEntityTypes.RecurringTask,
                template.Id,
                isActive ? AuditActions.Resumed : AuditActions.Paused,
                userId,
                new { template.NextRunAtUtc },
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await LoadDtoAsync(template.Id, unitId, cancellationToken);
    }

    private async Task<ValidatedDefinition> ValidateDefinitionAsync(
        RecurringTaskDefinitionDto dto,
        Guid unitId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new BusinessException("Tiêu đề không được để trống.");
        if (dto.Title.Trim().Length > 200)
            throw new BusinessException("Tiêu đề tối đa 200 ký tự.");
        if ((dto.Description?.Trim().Length ?? 0) > 1000)
            throw new BusinessException("Mô tả tối đa 1000 ký tự.");
        if (dto.PlannedEffortHours is <= 0m or > 100000m)
            throw new BusinessException("Khối lượng kế hoạch phải lớn hơn 0 và không vượt quá 100000 giờ.");

        var recurrenceType = ParseRecurrenceType(dto.RecurrenceType);
        var priority = ParsePriority(dto.Priority);
        var nextRunAtUtc = dto.NextRunAtUtc.UtcDateTime;
        _scheduleCalculator.Validate(
            recurrenceType,
            dto.Interval,
            dto.DayOfWeek,
            dto.DayOfMonth,
            nextRunAtUtc);
        await EnsureProjectIsAvailableAsync(dto.ProjectId, unitId, cancellationToken);

        var assigneeUserIds = await ValidateExplicitAssigneesAsync(
            dto.UserIds,
            unitId,
            cancellationToken);

        return new ValidatedDefinition(
            recurrenceType,
            priority,
            nextRunAtUtc,
            assigneeUserIds);
    }

    private async Task<List<Guid>> ValidateExplicitAssigneesAsync(
        IEnumerable<Guid> userIds,
        Guid unitId,
        CancellationToken cancellationToken)
    {
        var normalizedUserIds = (userIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (normalizedUserIds.Count == 0)
            return normalizedUserIds;

        var plan = await _taskRules.ResolveAssignmentPlan(
            normalizedUserIds,
            Array.Empty<Guid>(),
            unitId,
            cancellationToken);
        return plan.UserIds.ToList();
    }

    private async Task EnsureProjectIsAvailableAsync(
        Guid? projectId,
        Guid unitId,
        CancellationToken cancellationToken)
    {
        if (!projectId.HasValue)
            return;

        var project = await _context.Projects
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId.Value, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy dự án.");

        if (project.IsArchived)
            throw new BusinessException("Dự án đã được lưu trữ, không thể gắn lịch công việc định kỳ.");
        if (project.UnitId != unitId)
            throw new ForbiddenException("Lịch công việc và dự án phải thuộc cùng một phòng ban.");
    }

    private async Task<User> GetManagerAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == userId && candidate.IsApproved && !candidate.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        if (user.Role != SystemRoles.Manager)
            throw new ForbiddenException("Chỉ Trưởng phòng mới được quản lý lịch công việc định kỳ.");
        if (!user.UnitId.HasValue)
            throw new BusinessException("Trưởng phòng chưa thuộc phòng ban nào.");

        return user;
    }

    private async Task<RecurringTaskTemplate> GetManagedTemplateAsync(
        Guid id,
        Guid unitId,
        CancellationToken cancellationToken)
    {
        return await _context.RecurringTaskTemplates
            .Include(template => template.Assignees)
            .FirstOrDefaultAsync(
                template => template.Id == id && template.UnitId == unitId,
                cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lịch công việc định kỳ.");
    }

    private IQueryable<RecurringTaskTemplate> TemplateQuery()
    {
        return _context.RecurringTaskTemplates
            .AsNoTracking()
            .Include(template => template.Assignees)
            .ThenInclude(assignee => assignee.User);
    }

    private async Task<RecurringTaskTemplateDto> LoadDtoAsync(
        Guid id,
        Guid unitId,
        CancellationToken cancellationToken)
    {
        var template = await TemplateQuery()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id && candidate.UnitId == unitId,
                cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lịch công việc định kỳ.");
        return Map(template);
    }

    private void AddAssignees(Guid templateId, IEnumerable<Guid> userIds)
    {
        foreach (var userId in userIds)
        {
            _context.RecurringTaskAssignees.Add(new RecurringTaskAssignee
            {
                Id = Guid.NewGuid(),
                TemplateId = templateId,
                UserId = userId
            });
        }
    }

    private void ReplaceAssignees(
        RecurringTaskTemplate template,
        IReadOnlyCollection<Guid> newUserIds)
    {
        var newUserIdSet = newUserIds.ToHashSet();
        var removedAssignees = template.Assignees
            .Where(assignee => !newUserIdSet.Contains(assignee.UserId))
            .ToList();
        _context.RecurringTaskAssignees.RemoveRange(removedAssignees);

        var existingUserIds = template.Assignees
            .Select(assignee => assignee.UserId)
            .ToHashSet();
        AddAssignees(
            template.Id,
            newUserIdSet.Where(userId => !existingUserIds.Contains(userId)));
    }

    private static RecurringTaskTemplateDto Map(RecurringTaskTemplate template)
    {
        return new RecurringTaskTemplateDto
        {
            Id = template.Id,
            UnitId = template.UnitId
                ?? throw new InvalidOperationException("Lịch công việc định kỳ không có phòng ban."),
            ProjectId = template.ProjectId,
            Title = template.Title,
            Description = template.Description,
            Priority = template.Priority.ToString(),
            RequiresReview = template.RequiresReview,
            PlannedEffortHours = template.PlannedEffortHours,
            RecurrenceType = template.RecurrenceType.ToString(),
            Interval = template.Interval,
            DayOfWeek = template.DayOfWeek,
            DayOfMonth = template.DayOfMonth,
            NextRunAtUtc = template.NextRunAtUtc,
            LastGeneratedAtUtc = template.LastGeneratedAtUtc,
            IsActive = template.IsActive,
            CreatedByUserId = template.CreatedByUserId,
            CreatedAtUtc = template.CreatedAtUtc,
            Assignees = template.Assignees
                .Where(assignee => assignee.User != null)
                .OrderBy(assignee => assignee.User!.FullName)
                .Select(assignee => new RecurringTaskAssigneeDto
                {
                    Id = assignee.UserId,
                    FullName = assignee.User!.FullName ?? string.Empty,
                    EmployeeCode = assignee.User.EmployeeCode ?? string.Empty
                })
                .ToList(),
            RowVersion = template.RowVersion
        };
    }

    private static RecurrenceType ParseRecurrenceType(string value)
    {
        if (Enum.TryParse<RecurrenceType>(value, true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;
        throw new BusinessException("Loại lịch lặp không hợp lệ.");
    }

    private static TaskPriority ParsePriority(string? value)
    {
        if (Enum.TryParse<TaskPriority>(value, true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;
        throw new BusinessException("Mức ưu tiên không hợp lệ.");
    }

    private sealed record ValidatedDefinition(
        RecurrenceType RecurrenceType,
        TaskPriority Priority,
        DateTime NextRunAtUtc,
        List<Guid> AssigneeUserIds);
}
