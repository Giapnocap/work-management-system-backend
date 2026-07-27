using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Application.Services
{
    public class TaskAccessService : ITaskAccessService
    {
        private readonly AppDbContext _context;

        public TaskAccessService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string?> GetUserRole(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => u.Id == userId && u.IsApproved && !u.IsDeleted)
                .Select(u => u.Role)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Guid?> GetUserUnitId(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => u.Id == userId && u.IsApproved && !u.IsDeleted)
                .Select(u => u.UnitId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> CanManageUnit(
            Guid unitId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsApproved && !u.IsDeleted, cancellationToken);
            if (user == null) return false;
            if (user.Role == "Admin") return true;
            return user.Role == "Manager" && user.UnitId == unitId;
        }

        public async Task<bool> CanAccessTask(
            Guid taskId,
            Guid userId,
            bool managerOrCreatorOnly = false,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsApproved && !u.IsDeleted, cancellationToken);
            if (user == null) return false;
            if (user.Role == "Admin") return true;

            var task = await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == taskId && !t.IsDeleted, cancellationToken);
            if (task == null) return false;

            if (user.Role == "Manager")
                return user.UnitId.HasValue && task.UnitId == user.UnitId.Value;

            if (task.CreatedBy == userId) return true;

            if (managerOrCreatorOnly) return false;

            var direct = await _context.TaskAssignees
                .AnyAsync(a => a.TaskId == taskId && a.UserId == userId, cancellationToken);
            if (direct) return true;

            var hasDirectAssignees = await _context.TaskAssignees
                .AnyAsync(a => a.TaskId == taskId && a.UserId.HasValue, cancellationToken);
            if (hasDirectAssignees) return false;

            return user.UnitId.HasValue && await _context.TaskAssignees
                .AnyAsync(a => a.TaskId == taskId && a.UnitId == user.UnitId.Value, cancellationToken);
        }

        public async Task<bool> CanAccessUpload(
            Guid uploadId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var file = await _context.UploadFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == uploadId, cancellationToken);
            if (file == null) return false;

            return await CanAccessTask(
                file.TaskId,
                userId,
                cancellationToken: cancellationToken);
        }
    }
}
