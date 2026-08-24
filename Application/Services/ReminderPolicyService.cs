using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Application.Services;

public sealed class ReminderPolicyService : IReminderPolicyService
{
    private readonly IAppDbContext _context;
    private readonly ITransactionManager _transactionManager;
    private readonly IAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    public ReminderPolicyService(
        IAppDbContext context,
        ITransactionManager transactionManager,
        IAuditService auditService,
        TimeProvider timeProvider)
    {
        _context = context;
        _transactionManager = transactionManager;
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    public async Task<List<ReminderPolicyDto>> GetAsync(
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        var requester = await GetRequesterAsync(requesterId, cancellationToken);
        var query = PolicyQuery();

        if (requester.Role == SystemRoles.Manager)
        {
            if (!requester.UnitId.HasValue)
                throw new BusinessException("Trưởng phòng chưa thuộc phòng ban nào.");

            var unitId = requester.UnitId.Value;
            query = query.Where(policy =>
                policy.ScopeType == ReminderPolicyScope.Global ||
                policy.UnitId == unitId ||
                (policy.ProjectId.HasValue && _context.Projects
                    .IgnoreQueryFilters()
                    .Any(project => project.Id == policy.ProjectId.Value && project.UnitId == unitId)));
        }
        else if (requester.Role != SystemRoles.Admin)
        {
            throw new ForbiddenException("Chỉ Admin hoặc Trưởng phòng mới được xem chính sách nhắc hạn.");
        }

        var policies = await query
            .OrderBy(policy => policy.ScopeType)
            .ThenBy(policy => policy.Unit!.Name)
            .ThenBy(policy => policy.Project!.Name)
            .ThenBy(policy => policy.Id)
            .ToListAsync(cancellationToken);

        return policies.Select(Map).ToList();
    }

    public Task<ReminderPolicyDto> UpsertAsync(
        Guid id,
        UpsertReminderPolicyDto dto,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new BusinessException("Mã chính sách không hợp lệ.");

        return _transactionManager.ExecuteSerializableAsync(
            token => UpsertCoreAsync(id, dto, requesterId, token),
            cancellationToken);
    }

    private async Task<ReminderPolicyDto> UpsertCoreAsync(
        Guid id,
        UpsertReminderPolicyDto dto,
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        var requester = await GetRequesterAsync(requesterId, cancellationToken);
        if (requester.Role is not (SystemRoles.Admin or SystemRoles.Manager))
            throw new ForbiddenException("Chỉ Admin hoặc Trưởng phòng mới được quản lý chính sách nhắc hạn.");

        ValidateMilestones(dto);
        var scopeType = ParseScope(dto.ScopeType);
        var target = await ResolveTargetAsync(scopeType, dto.ScopeId, cancellationToken);
        EnsureCanManage(requester, scopeType, target.UnitId);

        var policy = await _context.ReminderPolicies
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        var isNew = policy == null;
        if (policy == null)
        {
            policy = new ReminderPolicy
            {
                Id = id,
                CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
            };
            _context.ReminderPolicies.Add(policy);
        }
        else
        {
            var existingUnitId = await ResolvePolicyUnitIdAsync(policy, cancellationToken);
            EnsureCanManage(requester, policy.ScopeType, existingUnitId);
            _context.SetOriginalRowVersion(policy, ConcurrencyToken.Require(dto.RowVersion));
        }

        var duplicateExists = await _context.ReminderPolicies
            .AsNoTracking()
            .AnyAsync(candidate =>
                candidate.Id != id &&
                candidate.ScopeType == scopeType &&
                candidate.UnitId == target.PolicyUnitId &&
                candidate.ProjectId == target.ProjectId,
                cancellationToken);
        if (duplicateExists)
            throw new BusinessException("Phạm vi này đã có chính sách nhắc hạn.");

        policy.ScopeType = scopeType;
        policy.UnitId = target.PolicyUnitId;
        policy.ProjectId = target.ProjectId;
        policy.BeforeDueHours = dto.BeforeDueHours;
        policy.OverdueEscalationHours = dto.OverdueEscalationHours;
        policy.NotifyAssignee = dto.NotifyAssignee;
        policy.NotifyManager = dto.NotifyManager;
        policy.IsActive = dto.IsActive;
        policy.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _auditService.RecordAsync(
            AuditEntityTypes.ReminderPolicy,
            policy.Id,
            isNew ? AuditActions.Created : AuditActions.Updated,
            requesterId,
            new
            {
                ScopeType = scopeType.ToString(),
                ScopeId = dto.ScopeId,
                policy.BeforeDueHours,
                policy.OverdueEscalationHours,
                policy.NotifyAssignee,
                policy.NotifyManager,
                policy.IsActive
            },
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await PolicyQuery()
            .Where(candidate => candidate.Id == policy.Id)
            .Select(candidate => new ReminderPolicyDto
            {
                Id = candidate.Id,
                ScopeType = candidate.ScopeType.ToString(),
                ScopeId = candidate.UnitId ?? candidate.ProjectId,
                ScopeName = candidate.ScopeType == ReminderPolicyScope.Global
                    ? "Toàn hệ thống"
                    : candidate.ScopeType == ReminderPolicyScope.Unit
                        ? candidate.Unit!.Name
                        : candidate.Project!.Name,
                BeforeDueHours = candidate.BeforeDueHours,
                OverdueEscalationHours = candidate.OverdueEscalationHours,
                NotifyAssignee = candidate.NotifyAssignee,
                NotifyManager = candidate.NotifyManager,
                IsActive = candidate.IsActive,
                CreatedAtUtc = candidate.CreatedAtUtc,
                UpdatedAtUtc = candidate.UpdatedAtUtc,
                RowVersion = candidate.RowVersion
            })
            .SingleAsync(cancellationToken);
    }

    private async Task<PolicyTarget> ResolveTargetAsync(
        ReminderPolicyScope scopeType,
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        if (scopeType == ReminderPolicyScope.Global)
        {
            if (scopeId.HasValue)
                throw new BusinessException("Chính sách toàn hệ thống không được có ScopeId.");
            return new PolicyTarget(null, null, null);
        }

        if (!scopeId.HasValue || scopeId.Value == Guid.Empty)
            throw new BusinessException("Chính sách phòng ban hoặc dự án phải có ScopeId hợp lệ.");

        if (scopeType == ReminderPolicyScope.Unit)
        {
            var unitExists = await _context.Units
                .AsNoTracking()
                .AnyAsync(unit => unit.Id == scopeId.Value, cancellationToken);
            if (!unitExists)
                throw new NotFoundException("Không tìm thấy phòng ban của chính sách.");

            return new PolicyTarget(scopeId.Value, scopeId.Value, null);
        }

        var project = await _context.Projects
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(candidate => candidate.Id == scopeId.Value)
            .Select(candidate => new { candidate.UnitId, candidate.IsArchived })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy dự án của chính sách.");
        if (project.IsArchived)
            throw new BusinessException("Không thể tạo chính sách cho dự án đã lưu trữ.");
        if (!project.UnitId.HasValue)
            throw new BusinessException("Dự án chưa thuộc phòng ban hợp lệ.");

        return new PolicyTarget(project.UnitId.Value, null, scopeId.Value);
    }

    private async Task<Guid?> ResolvePolicyUnitIdAsync(
        ReminderPolicy policy,
        CancellationToken cancellationToken)
    {
        if (policy.ScopeType == ReminderPolicyScope.Global)
            return null;
        if (policy.ScopeType == ReminderPolicyScope.Unit)
            return policy.UnitId;
        if (!policy.ProjectId.HasValue)
            return null;

        return await _context.Projects
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(project => project.Id == policy.ProjectId.Value)
            .Select(project => project.UnitId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static void EnsureCanManage(
        Requester requester,
        ReminderPolicyScope scopeType,
        Guid? targetUnitId)
    {
        if (requester.Role == SystemRoles.Admin)
            return;
        if (requester.Role != SystemRoles.Manager ||
            scopeType == ReminderPolicyScope.Global ||
            !requester.UnitId.HasValue ||
            requester.UnitId != targetUnitId)
        {
            throw new ForbiddenException("Trưởng phòng chỉ được quản lý chính sách của phòng và dự án thuộc phòng mình.");
        }
    }

    private async Task<Requester> GetRequesterAsync(
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == requesterId && user.IsApproved && !user.IsDeleted)
            .Select(user => new Requester(user.Role, user.UnitId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");
    }

    private IQueryable<ReminderPolicy> PolicyQuery()
    {
        return _context.ReminderPolicies
            .AsNoTracking()
            .Include(policy => policy.Unit)
            .Include(policy => policy.Project);
    }

    private static ReminderPolicyDto Map(ReminderPolicy policy)
    {
        return new ReminderPolicyDto
        {
            Id = policy.Id,
            ScopeType = policy.ScopeType.ToString(),
            ScopeId = policy.UnitId ?? policy.ProjectId,
            ScopeName = policy.ScopeType switch
            {
                ReminderPolicyScope.Global => "Toàn hệ thống",
                ReminderPolicyScope.Unit => policy.Unit?.Name ?? string.Empty,
                ReminderPolicyScope.Project => policy.Project?.Name ?? string.Empty,
                _ => string.Empty
            },
            BeforeDueHours = policy.BeforeDueHours,
            OverdueEscalationHours = policy.OverdueEscalationHours,
            NotifyAssignee = policy.NotifyAssignee,
            NotifyManager = policy.NotifyManager,
            IsActive = policy.IsActive,
            CreatedAtUtc = policy.CreatedAtUtc,
            UpdatedAtUtc = policy.UpdatedAtUtc,
            RowVersion = policy.RowVersion
        };
    }

    private static ReminderPolicyScope ParseScope(string value)
    {
        if (Enum.TryParse<ReminderPolicyScope>(value, true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        throw new BusinessException("Phạm vi chính sách nhắc hạn không hợp lệ.");
    }

    private static void ValidateMilestones(UpsertReminderPolicyDto dto)
    {
        if (dto.BeforeDueHours is < 1 or > DeadlineReminderOptions.MaxPolicyHours)
            throw new BusinessException("Mốc nhắc trước hạn phải từ 1 đến 720 giờ.");
        if (dto.OverdueEscalationHours is < 0 or > DeadlineReminderOptions.MaxPolicyHours)
            throw new BusinessException("Mốc cảnh báo quá hạn phải từ 0 đến 720 giờ.");
    }

    private sealed record Requester(string Role, Guid? UnitId);
    private sealed record PolicyTarget(Guid? UnitId, Guid? PolicyUnitId, Guid? ProjectId);
}
