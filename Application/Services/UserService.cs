using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IGenericRepository<User> _repo;
        private readonly IGenericRepository<UserUnit> _userUnitRepo;
        private readonly IMapper _mapper;
        private readonly IUserTaskAssignmentService _taskAssignmentService;
        private readonly IStaffMovementService _staffMovementService;
        private readonly IUserPerformanceService _performanceService;
        private readonly ITransactionManager _transactionManager;
        private readonly IAuditService _auditService;
        private readonly IAppDbContext _context;

        public UserService(
            IGenericRepository<User> repo,
            IGenericRepository<UserUnit> userUnitRepo,
            IMapper mapper,
            IUserTaskAssignmentService taskAssignmentService,
            IStaffMovementService staffMovementService,
            IUserPerformanceService performanceService,
            ITransactionManager transactionManager,
            IAuditService auditService,
            IAppDbContext context)
        {
            _repo = repo;
            _userUnitRepo = userUnitRepo;
            _mapper = mapper;
            _taskAssignmentService = taskAssignmentService;
            _staffMovementService = staffMovementService;
            _performanceService = performanceService;
            _transactionManager = transactionManager;
            _auditService = auditService;
            _context = context;
        }

        public async Task<List<UserDto>> GetAll(CancellationToken cancellationToken = default)
            => _mapper.Map<List<UserDto>>(await _repo.QueryReadOnly()
                .Where(u => !u.IsDeleted && u.IsApproved)
                .ToListAsync(cancellationToken));

        public async Task<Guid?> GetUnitIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _repo.GetByIdAsync(userId, cancellationToken);
            return user?.UnitId;
        }

        public async Task<bool> IsUserActive(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _repo.GetByIdAsync(userId, cancellationToken);
            return user != null && user.IsApproved && !user.IsDeleted;
        }

        public async Task<List<UserDto>> GetByManager(Guid managerId, CancellationToken cancellationToken = default)
        {
            var manager = await _repo.GetByIdAsync(managerId, cancellationToken);
            if (manager?.UnitId == null) return new List<UserDto>();

            var unitId = manager.UnitId.Value;
            var mappedUserIds = _userUnitRepo.QueryReadOnly()
                .Where(uu => uu.UnitId == unitId)
                .Select(uu => uu.UserId);

            var users = await _repo.QueryReadOnly()
                .Where(u => (u.UnitId == unitId || mappedUserIds.Contains(u.Id) || u.UnitId == null)
                            && u.Role != SystemRoles.Admin
                            && u.IsApproved
                            && !u.IsDeleted)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<UserDto>>(users);
        }

        public async Task<List<UserDto>> GetVisibleUsers(
            Guid requesterId,
            CancellationToken cancellationToken = default)
        {
            var requester = await GetActiveRequester(requesterId, cancellationToken);
            return requester.Role switch
            {
                SystemRoles.Admin => await GetAll(cancellationToken),
                SystemRoles.Manager => await GetByManager(requesterId, cancellationToken),
                _ => throw new ForbiddenException("Bạn không có quyền xem danh sách nhân sự.")
            };
        }

        public async Task<List<UserDto>> Search(
            string keyword,
            string? role,
            Guid? unitId,
            Guid? managerId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _repo.QueryReadOnly().Where(u => u.Role != SystemRoles.Admin && u.IsApproved && !u.IsDeleted);

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.Contains(keyword)) ||
                    (u.EmployeeCode != null && u.EmployeeCode.Contains(keyword)) ||
                    u.Username.Contains(keyword));

            if (!string.IsNullOrEmpty(role))
                query = query.Where(u => u.Role == role);

            if (managerId.HasValue)
            {
                var m = await _repo.GetByIdAsync(managerId.Value, cancellationToken);
                if (m != null && m.UnitId.HasValue)
                {
                    var muId = m.UnitId.Value;
                    query = query.Where(u => u.UnitId == muId || u.UnitId == null);
                }
            }
            else if (unitId.HasValue)
            {
                query = query.Where(u => u.UnitId == unitId.Value);
            }

            return _mapper.Map<List<UserDto>>(await query.ToListAsync(cancellationToken));
        }

        public async Task<List<UserDto>> SearchVisibleUsers(
            Guid requesterId,
            string keyword,
            string? role,
            Guid? unitId,
            CancellationToken cancellationToken = default)
        {
            var requester = await GetActiveRequester(requesterId, cancellationToken);
            return requester.Role switch
            {
                SystemRoles.Admin => await Search(keyword, role, unitId, cancellationToken: cancellationToken),
                SystemRoles.Manager => await Search(keyword, role, null, requesterId, cancellationToken),
                _ => throw new ForbiddenException("Bạn không có quyền tìm kiếm nhân sự.")
            };
        }

        public Task<UserDto> Update(
            Guid id,
            UpdateUserDto dto,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                token => UpdateCore(id, dto, changedBy, token),
                cancellationToken);

        private async Task<UserDto> UpdateCore(
            Guid id,
            UpdateUserDto dto,
            Guid? changedBy,
            CancellationToken cancellationToken)
        {
            var user = await _repo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy người dùng.");

            var now = DateTime.UtcNow;
            var replacement = await ResolveManagerReplacementAsync(user, dto, cancellationToken);

            await _staffMovementService.ValidateChangeAsync(
                user,
                dto.Role,
                dto.UnitId,
                replacement?.Manager.Id,
                cancellationToken);

            if (replacement != null)
            {
                await _staffMovementService.ValidateChangeAsync(
                    replacement.Manager,
                    SystemRoles.User,
                    replacement.NewUnitId,
                    cancellationToken: cancellationToken);

                await _staffMovementService.ApplyChangeAsync(
                    replacement.Manager,
                    SystemRoles.User,
                    replacement.NewUnitId,
                    changedBy,
                    "Thay thế Trưởng phòng",
                    now,
                    cancellationToken: cancellationToken);
            }

            await _staffMovementService.ApplyChangeAsync(
                user,
                dto.Role,
                dto.UnitId,
                changedBy,
                "Admin cập nhật phân công nhân sự",
                now,
                replacement?.Manager.Id,
                cancellationToken);

            _context.SetOriginalRowVersion(user, ConcurrencyToken.Require(dto.RowVersion));
            await _context.SaveChangesAsync(cancellationToken);
            return _mapper.Map<UserDto>(user);
        }

        private async Task<ManagerReplacement?> ResolveManagerReplacementAsync(
            User targetUser,
            UpdateUserDto dto,
            CancellationToken cancellationToken)
        {
            var hasReplacementInput = dto.OldManagerId.HasValue ||
                                      !string.IsNullOrWhiteSpace(dto.OldManagerAction) ||
                                      dto.OldManagerNewUnitId.HasValue;

            if (dto.Role != SystemRoles.Manager || !dto.UnitId.HasValue)
            {
                if (hasReplacementInput)
                    throw new BusinessException("Chỉ được gửi thông tin thay Trưởng phòng khi bổ nhiệm vai trò Manager.");

                return null;
            }

            var managerIds = await _repo.QueryReadOnly()
                .Where(candidate =>
                    candidate.Id != targetUser.Id &&
                    candidate.Role == SystemRoles.Manager &&
                    candidate.UnitId == dto.UnitId &&
                    candidate.IsApproved &&
                    !candidate.IsDeleted)
                .Select(candidate => candidate.Id)
                .ToListAsync(cancellationToken);

            if (managerIds.Count > 1)
                throw new BusinessException("Dữ liệu phòng ban không hợp lệ: có nhiều hơn một Trưởng phòng.");

            if (managerIds.Count == 0)
            {
                if (hasReplacementInput)
                    throw new BusinessException("Phòng ban không có Trưởng phòng cần thay thế.");

                return null;
            }

            var existingManagerId = managerIds[0];
            if (dto.OldManagerId != existingManagerId)
                throw new BusinessException("OldManagerId không khớp với Trưởng phòng hiện tại.");

            var existingManager = await _repo.GetByIdAsync(existingManagerId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy Trưởng phòng hiện tại.");

            if (dto.OldManagerAction == "Remove")
            {
                if (dto.OldManagerNewUnitId.HasValue)
                    throw new BusinessException("Không được gửi phòng ban mới khi chọn Remove.");

                return new ManagerReplacement(existingManager, null);
            }

            if (dto.OldManagerAction == "Transfer")
            {
                if (!dto.OldManagerNewUnitId.HasValue)
                    throw new BusinessException("Cần chọn phòng ban mới cho Trưởng phòng cũ.");

                if (dto.OldManagerNewUnitId == dto.UnitId)
                    throw new BusinessException("Trưởng phòng cũ phải được chuyển sang phòng ban khác.");

                return new ManagerReplacement(existingManager, dto.OldManagerNewUnitId);
            }

            throw new BusinessException("OldManagerAction chỉ có thể là Transfer hoặc Remove.");
        }

        public async Task Delete(
            Guid id,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default)
        {
            await _transactionManager.ExecuteSerializableAsync(
                async token =>
                {
                    await DeleteCore(id, changedBy, token);
                    return true;
                },
                cancellationToken);
        }

        private async Task DeleteCore(
            Guid id,
            Guid? changedBy,
            CancellationToken cancellationToken)
        {
            var user = await _repo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy người dùng.");

            await _taskAssignmentService.EnsureCanDeleteAsync(user, cancellationToken);

            var deletedAt = DateTime.UtcNow;
            await _staffMovementService.DeactivateAsync(
                user,
                changedBy,
                deletedAt,
                cancellationToken);

            user.IsDeleted = true;
            user.InvalidateSessions();
            _repo.Update(user);
            await _auditService.RecordAsync(
                AuditEntityTypes.Account,
                user.Id,
                AuditActions.Deleted,
                changedBy,
                new { user.Role, user.UnitId },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task<PerformanceDto> GetPerformanceAsync(
            Guid userId,
            Guid? periodId = null,
            CancellationToken cancellationToken = default)
        {
            return _performanceService.GetPerformanceAsync(userId, periodId, cancellationToken);
        }

        public async Task<PerformanceDto> GetVisiblePerformanceAsync(
            Guid requesterId,
            Guid targetUserId,
            Guid? periodId = null,
            CancellationToken cancellationToken = default)
        {
            if (!await CanViewPerformanceAsync(requesterId, targetUserId, periodId, cancellationToken))
                throw new ForbiddenException("Bạn không có quyền xem KPI của nhân sự này.");

            return await GetPerformanceAsync(targetUserId, periodId, cancellationToken);
        }

        public Task<bool> CanViewPerformanceAsync(
            Guid requesterId,
            Guid targetUserId,
            Guid? periodId = null,
            CancellationToken cancellationToken = default)
        {
            return _performanceService.CanViewPerformanceAsync(
                requesterId,
                targetUserId,
                periodId,
                cancellationToken);
        }

        public Task<List<PerformanceDto>> GetUnitPerformanceAsync(
            Guid requesterId,
            Guid? periodId = null,
            CancellationToken cancellationToken = default)
        {
            return _performanceService.GetUnitPerformanceAsync(requesterId, periodId, cancellationToken);
        }

        private async Task<User> GetActiveRequester(
            Guid requesterId,
            CancellationToken cancellationToken)
        {
            return await _repo.QueryReadOnly()
                .FirstOrDefaultAsync(user =>
                    user.Id == requesterId &&
                    user.IsApproved &&
                    !user.IsDeleted,
                    cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy người dùng.");
        }

        private sealed record ManagerReplacement(User Manager, Guid? NewUnitId);
    }
}
