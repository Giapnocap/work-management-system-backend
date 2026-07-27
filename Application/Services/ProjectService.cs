using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class ProjectService : IProjectService
    {
        private static readonly TaskStatusEnum[] ProjectStatuses =
        {
            TaskStatusEnum.NotStarted,
            TaskStatusEnum.InProgress,
            TaskStatusEnum.Submitted,
            TaskStatusEnum.Approved
        };

        private readonly AppDbContext _context;
        private readonly ITaskAccessService _accessService;
        private readonly ITransactionManager _transactionManager;
        private readonly IAuditService _auditService;

        public ProjectService(
            AppDbContext context,
            ITaskAccessService accessService,
            ITransactionManager transactionManager,
            IAuditService auditService)
        {
            _context = context;
            _accessService = accessService;
            _transactionManager = transactionManager;
            _auditService = auditService;
        }

        public async Task<List<ProjectDto>> GetProjects(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Id == userId && u.IsApproved && !u.IsDeleted,
                    cancellationToken)
                ?? throw new NotFoundException("User not found.");

            if (user.Role != "Manager")
                throw new ForbiddenException("Only managers can view projects.");

            if (!user.UnitId.HasValue)
                throw new BusinessException("Manager chua thuoc phong ban nao.");

            var query = _context.Projects
                .Where(p => !p.IsArchived && p.UnitId == user.UnitId.Value);

            var projects = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
            var unitIds = projects.Where(p => p.UnitId.HasValue).Select(p => p.UnitId!.Value).Distinct().ToList();
            var units = await _context.Units
                .Where(u => unitIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

            var statusCountsByProject = await BuildStatusCountsByProject(
                projects.Select(p => p.Id).ToList(),
                cancellationToken);

            return projects.Select(p =>
            {
                var dto = MapProject(p, units);
                dto.StatusCounts = statusCountsByProject.TryGetValue(p.Id, out var counts) ? counts : BuildEmptyStatusCounts();
                return dto;
            }).ToList();
        }

        public Task<ProjectDto> CreateProject(
            CreateProjectDto dto,
            Guid userId,
            CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                token => CreateProjectCore(dto, userId, token),
                cancellationToken);

        private async Task<ProjectDto> CreateProjectCore(
            CreateProjectDto dto,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Id == userId && u.IsApproved && !u.IsDeleted,
                    cancellationToken)
                ?? throw new NotFoundException("User not found.");

            if (user.Role != "Manager")
                throw new ForbiddenException("Chi Manager moi duoc tao project.");

            if (!user.UnitId.HasValue)
                throw new BusinessException("Manager chua thuoc phong ban nao.");

            var unitId = user.UnitId.Value;
            if (dto.UnitId.HasValue && dto.UnitId.Value != unitId)
                throw new ForbiddenException("Ban khong co quyen tao project cho phong ban nay.");

            if (!await _accessService.CanManageUnit(unitId, userId, cancellationToken))
                throw new ForbiddenException("Ban khong co quyen tao project cho phong ban nay.");

            var name = dto.Name.Trim();
            var exists = await _context.Projects
                .IgnoreQueryFilters()
                .AnyAsync(
                    p => p.UnitId == unitId && p.Name == name,
                    cancellationToken);
            if (exists) throw new BusinessException("Project cung ten da ton tai trong phong ban.");

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = dto.Description?.Trim() ?? string.Empty,
                UnitId = unitId,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _auditService.RecordAsync(
                AuditEntityTypes.Project,
                project.Id,
                AuditActions.Created,
                userId,
                new { project.Name, project.UnitId },
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var units = await _context.Units
                .Where(u => u.Id == unitId)
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

            var result = MapProject(project, units);
            result.StatusCounts = BuildEmptyStatusCounts();
            return result;
        }

        public Task<ProjectDto> UpdateProject(
            Guid id,
            CreateProjectDto dto,
            Guid userId,
            CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                token => UpdateProjectCore(id, dto, userId, token),
                cancellationToken);

        private async Task<ProjectDto> UpdateProjectCore(
            Guid id,
            CreateProjectDto dto,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(
                    p => p.Id == id && !p.IsArchived,
                    cancellationToken)
                ?? throw new NotFoundException("Project not found.");

            if (!await CanManageProject(project, userId, cancellationToken))
                throw new ForbiddenException("Ban khong co quyen cap nhat project nay.");

            if (dto.UnitId.HasValue && dto.UnitId != project.UnitId)
                throw new BusinessException("Khong the thay doi phong ban cua project sau khi tao.");

            var name = dto.Name.Trim();
            var duplicateName = await _context.Projects
                .IgnoreQueryFilters()
                .AnyAsync(
                    candidate =>
                        candidate.Id != project.Id &&
                        candidate.UnitId == project.UnitId &&
                        candidate.Name == name,
                    cancellationToken);
            if (duplicateName)
                throw new BusinessException("Project cung ten da ton tai trong phong ban.");

            var oldName = project.Name;
            var oldDescription = project.Description;
            var newDescription = dto.Description?.Trim() ?? string.Empty;

            project.Name = name;
            project.Description = newDescription;

            if (oldName != name || oldDescription != newDescription)
            {
                await _auditService.RecordAsync(
                    AuditEntityTypes.Project,
                    project.Id,
                    AuditActions.Updated,
                    userId,
                    new
                    {
                        oldName,
                        newName = name,
                        oldDescription,
                        newDescription
                    },
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var units = project.UnitId.HasValue
                ? await _context.Units
                    .Where(u => u.Id == project.UnitId.Value)
                    .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken)
                : new Dictionary<Guid, string>();

            var statusCounts = await BuildStatusCountsByProject(
                new List<Guid> { project.Id },
                cancellationToken);
            var result = MapProject(project, units);
            result.StatusCounts = statusCounts.TryGetValue(project.Id, out var counts) ? counts : BuildEmptyStatusCounts();
            return result;
        }

        public Task ArchiveProject(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                async token =>
                {
                    await ArchiveProjectCore(id, userId, token);
                    return true;
                },
                cancellationToken);

        private async Task ArchiveProjectCore(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(
                    p => p.Id == id && !p.IsArchived,
                    cancellationToken)
                ?? throw new NotFoundException("Project not found.");

            if (!await CanManageProject(project, userId, cancellationToken))
                throw new ForbiddenException("Ban khong co quyen luu tru project nay.");

            var activeTaskCount = await _context.Tasks.CountAsync(
                task =>
                    task.ProjectId == project.Id &&
                    !task.IsDeleted &&
                    task.Status != TaskStatusEnum.Approved,
                cancellationToken);

            if (activeTaskCount > 0)
            {
                throw new BusinessException(
                    $"Khong the luu tru project khi con {activeTaskCount} cong viec chua hoan thanh.");
            }

            project.IsArchived = true;
            await _auditService.RecordAsync(
                AuditEntityTypes.Project,
                project.Id,
                AuditActions.Archived,
                userId,
                new { project.Name, project.UnitId },
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<bool> CanManageProject(
            Project project,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Id == userId && u.IsApproved && !u.IsDeleted,
                    cancellationToken);
            if (user == null) return false;
            if (user.Role != "Manager") return false;
            return project.UnitId.HasValue &&
                   user.UnitId == project.UnitId &&
                   await _accessService.CanManageUnit(project.UnitId.Value, userId, cancellationToken);
        }

        private static ProjectDto MapProject(Project project, Dictionary<Guid, string> units)
        {
            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                UnitId = project.UnitId,
                UnitName = project.UnitId.HasValue && units.TryGetValue(project.UnitId.Value, out var unitName) ? unitName : null,
                CreatedBy = project.CreatedBy,
                CreatedAt = project.CreatedAt,
                IsArchived = project.IsArchived
            };
        }

        private async Task<Dictionary<Guid, List<ProjectStatusCountDto>>> BuildStatusCountsByProject(
            List<Guid> projectIds,
            CancellationToken cancellationToken)
        {
            if (!projectIds.Any())
                return new Dictionary<Guid, List<ProjectStatusCountDto>>();

            var taskCountRows = await _context.Tasks
                .Where(t => t.ProjectId.HasValue && projectIds.Contains(t.ProjectId.Value))
                .GroupBy(t => new { ProjectId = t.ProjectId!.Value, t.Status })
                .Select(g => new { g.Key.ProjectId, g.Key.Status, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var taskCounts = taskCountRows.ToDictionary(
                x => (x.ProjectId, x.Status),
                x => x.Count);

            return projectIds.ToDictionary(
                projectId => projectId,
                projectId => ProjectStatuses.Select(status => new ProjectStatusCountDto
                {
                    Status = status.ToString(),
                    Label = GetStatusLabel(status),
                    Count = taskCounts.TryGetValue((projectId, status), out var count) ? count : 0
                }).ToList());
        }

        private static List<ProjectStatusCountDto> BuildEmptyStatusCounts()
        {
            return ProjectStatuses.Select(status => new ProjectStatusCountDto
            {
                Status = status.ToString(),
                Label = GetStatusLabel(status),
                Count = 0
            }).ToList();
        }

        private static string GetStatusLabel(TaskStatusEnum status)
        {
            return status switch
            {
                TaskStatusEnum.NotStarted => "Chua bat dau",
                TaskStatusEnum.InProgress => "Dang lam",
                TaskStatusEnum.Submitted => "Cho duyet",
                TaskStatusEnum.Approved => "Hoan thanh",
                _ => status.ToString()
            };
        }
    }
}
