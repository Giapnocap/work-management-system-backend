using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Services
{
    public sealed class StaffMovementService : IStaffMovementService
    {
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<Unit> _unitRepo;
        private readonly IUserTaskAssignmentService _taskAssignmentService;
        private readonly IUserUnitMembershipService _membershipService;
        private readonly IUserWorkHistoryService _workHistoryService;
        private readonly IAuditService _auditService;

        public StaffMovementService(
            IGenericRepository<User> userRepo,
            IGenericRepository<Unit> unitRepo,
            IUserTaskAssignmentService taskAssignmentService,
            IUserUnitMembershipService membershipService,
            IUserWorkHistoryService workHistoryService,
            IAuditService auditService)
        {
            _userRepo = userRepo;
            _unitRepo = unitRepo;
            _taskAssignmentService = taskAssignmentService;
            _membershipService = membershipService;
            _workHistoryService = workHistoryService;
            _auditService = auditService;
        }

        public async Task ValidateChangeAsync(
            User user,
            string newRole,
            Guid? newUnitId,
            Guid? managerBeingReplacedId = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRoleChange(user, newRole, newUnitId);

            if (newUnitId.HasValue)
            {
                var unitExists = await _unitRepo.QueryReadOnly()
                    .AnyAsync(unit => unit.Id == newUnitId.Value && !unit.IsDeleted, cancellationToken);
                if (!unitExists)
                    throw new BusinessException("Phòng ban không tồn tại hoặc đã bị lưu trữ.");
            }

            if (newRole == SystemRoles.Manager)
            {
                var hasAnotherManager = await _userRepo.QueryReadOnly()
                    .AnyAsync(candidate =>
                        candidate.Id != user.Id &&
                        candidate.Id != managerBeingReplacedId &&
                        candidate.Role == SystemRoles.Manager &&
                        candidate.UnitId == newUnitId &&
                        candidate.IsApproved &&
                        !candidate.IsDeleted,
                        cancellationToken);

                if (hasAnotherManager)
                    throw new BusinessException("Phòng ban đã có Trưởng phòng. Cần xử lý Trưởng phòng hiện tại trước.");
            }

            await _taskAssignmentService.EnsureCanChangeAssignmentAsync(
                user,
                newRole,
                newUnitId,
                cancellationToken);
        }

        public async Task ApplyChangeAsync(
            User user,
            string newRole,
            Guid? newUnitId,
            Guid? changedBy,
            string reason,
            DateTime changedAt,
            Guid? managerBeingReplacedId = null,
            CancellationToken cancellationToken = default)
        {
            await ValidateChangeAsync(
                user,
                newRole,
                newUnitId,
                managerBeingReplacedId,
                cancellationToken);

            var unitChanged = user.UnitId != newUnitId;
            var roleChanged = user.Role != newRole;

            if (unitChanged || roleChanged)
            {
                var oldRole = user.Role;
                var oldUnitId = user.UnitId;

                await _workHistoryService.RecordChangeAsync(
                    user,
                    newUnitId,
                    newRole,
                    changedBy,
                    reason,
                    changedAt,
                    cancellationToken);

                user.Role = newRole;
                user.UnitId = newUnitId;
                if (unitChanged)
                    user.JoinedUnitAt = changedAt;
                user.InvalidateSessions();

                _userRepo.Update(user);
                await _auditService.RecordAsync(
                    AuditEntityTypes.Account,
                    user.Id,
                    AuditActions.AssignmentChanged,
                    changedBy,
                    new
                    {
                        OldRole = oldRole,
                        NewRole = newRole,
                        OldUnitId = oldUnitId,
                        NewUnitId = newUnitId,
                        Reason = reason
                    },
                    cancellationToken);
            }

            await _membershipService.ReplaceMembership(user.Id, newUnitId, cancellationToken);
        }

        public async Task DeactivateAsync(
            User user,
            Guid? changedBy,
            DateTime changedAt,
            CancellationToken cancellationToken = default)
        {
            await _workHistoryService.CloseCurrentAsync(
                user,
                changedBy,
                changedAt,
                cancellationToken);
            await _membershipService.ReplaceMembership(user.Id, null, cancellationToken);
        }

        private static void ValidateRoleChange(User user, string newRole, Guid? newUnitId)
        {
            if (user.Role == SystemRoles.Admin || newRole == SystemRoles.Admin)
                throw new BusinessException("Tài khoản Admin không được thay đổi qua luồng điều chuyển nhân sự.");

            if (newRole != SystemRoles.User && newRole != SystemRoles.Manager)
                throw new BusinessException("Chức vụ chỉ có thể là User hoặc Manager.");

            if (newRole == SystemRoles.Manager && !newUnitId.HasValue)
                throw new BusinessException("Trưởng phòng phải thuộc một phòng ban.");
        }
    }
}
