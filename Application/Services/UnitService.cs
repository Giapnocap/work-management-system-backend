using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;

namespace WorkManagementSystem.Application.Services
{
    public class UnitService : IUnitService
    {
        private readonly IGenericRepository<Unit> _repo;
        private readonly IGenericRepository<UserUnit> _userUnitRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IStaffMovementService _staffMovementService;
        private readonly IMapper _mapper;
        private readonly ITransactionManager _transactionManager;
        private readonly IAuditService _auditService;

        public UnitService(
            IGenericRepository<Unit> repo,
            IGenericRepository<UserUnit> userUnitRepo,
            IGenericRepository<User> userRepo,
            IStaffMovementService staffMovementService,
            IMapper mapper,
            ITransactionManager transactionManager,
            IAuditService auditService)
        {
            _repo = repo;
            _userUnitRepo = userUnitRepo;
            _userRepo = userRepo;
            _staffMovementService = staffMovementService;
            _mapper = mapper;
            _transactionManager = transactionManager;
            _auditService = auditService;
        }

        public async Task<List<UnitDto>> GetAll(CancellationToken cancellationToken = default)
            => _mapper.Map<List<UnitDto>>(await _repo.QueryReadOnly().ToListAsync(cancellationToken));

        public async Task<UnitDto?> GetMyUnit(Guid userId, CancellationToken cancellationToken = default)
        {
            var userUnit = await _userUnitRepo.QueryReadOnly()
                .Include(x => x.Unit)
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (userUnit != null)
                return _mapper.Map<UnitDto>(userUnit.Unit);

            var user = await _userRepo.GetByIdAsync(userId, cancellationToken);
            if (user?.UnitId != null)
            {
                var unit = await _repo.GetByIdAsync(user.UnitId.Value, cancellationToken);
                return _mapper.Map<UnitDto>(unit);
            }

            return null;
        }

        public async Task<List<UserDto>> GetUsers(Guid unitId, CancellationToken cancellationToken = default)
        {
            var userIdsFromMapping = await _userUnitRepo.QueryReadOnly()
                .Where(x => x.UnitId == unitId)
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken);

            var users = await _userRepo.QueryReadOnly()
                .Where(u => (u.UnitId == unitId || userIdsFromMapping.Contains(u.Id))
                            && u.IsApproved
                            && !u.IsDeleted)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FullName = u.FullName ?? "—",
                    EmployeeCode = u.EmployeeCode ?? "—",
                    Role = u.Role,
                    UnitId = u.UnitId ?? unitId,
                    IsApproved = u.IsApproved,
                    PhoneNumber = u.PhoneNumber
                })
                .ToListAsync(cancellationToken);

            return users;
        }

        public async Task<UnitDto> Create(
            CreateUnitDto dto,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default)
        {
            var exists = await _repo.QueryReadOnly().AnyAsync(u => u.Name == dto.Name, cancellationToken);
            if (exists) throw new BusinessException("Tên phòng ban đã tồn tại!");

            var unit = new Unit { Id = Guid.NewGuid(), Name = dto.Name };
            await _repo.AddAsync(unit, cancellationToken);
            await _auditService.RecordAsync(
                AuditEntityTypes.Unit,
                unit.Id,
                AuditActions.Created,
                changedBy,
                new { unit.Name },
                cancellationToken);
            await _repo.SaveAsync(cancellationToken);
            return _mapper.Map<UnitDto>(unit);
        }

        public async Task<UnitDto> Update(
            Guid id,
            CreateUnitDto dto,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default)
        {
            var exists = await _repo.QueryReadOnly().AnyAsync(u => u.Name == dto.Name && u.Id != id, cancellationToken);
            if (exists) throw new BusinessException("Tên phòng ban đã tồn tại!");

            var unit = await _repo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Unit not found");
            var oldName = unit.Name;
            unit.Name = dto.Name;
            _repo.Update(unit);
            if (oldName != unit.Name)
            {
                await _auditService.RecordAsync(
                    AuditEntityTypes.Unit,
                    unit.Id,
                    AuditActions.Updated,
                    changedBy,
                    new { OldName = oldName, NewName = unit.Name },
                    cancellationToken);
            }
            await _repo.SaveAsync(cancellationToken);
            return _mapper.Map<UnitDto>(unit);
        }

        public async Task Delete(
            Guid id,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default)
        {
            var unit = await _repo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Unit not found");

            var hasMembers = await _userUnitRepo.QueryReadOnly().AnyAsync(x => x.UnitId == id, cancellationToken)
                || await _userRepo.QueryReadOnly().AnyAsync(x => x.UnitId == id && x.IsApproved && !x.IsDeleted, cancellationToken);
            if (hasMembers)
            {
                throw new BusinessException("Không thể xóa! Phòng ban này vẫn đang có nhân sự. Vui lòng luân chuyển toàn bộ Quản lý và Nhân viên sang phòng khác hoặc gỡ tư cách thành viên của họ trước.");
            }

            unit.IsDeleted = true;
            _repo.Update(unit);
            await _auditService.RecordAsync(
                AuditEntityTypes.Unit,
                unit.Id,
                AuditActions.Deleted,
                changedBy,
                new { unit.Name },
                cancellationToken);
            await _repo.SaveAsync(cancellationToken);
        }

        public Task AddMember(
            Guid unitId,
            Guid userId,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                async token =>
                {
                    await AddMemberCore(unitId, userId, changedBy, token);
                    return true;
                },
                cancellationToken);

        private async Task AddMemberCore(
            Guid unitId,
            Guid userId,
            Guid? changedBy,
            CancellationToken cancellationToken)
        {
            _ = await _repo.GetByIdAsync(unitId, cancellationToken)
                ?? throw new NotFoundException("Unit not found");
            var user = await _userRepo.GetByIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException("User not found");

            var exists = await _userUnitRepo.QueryReadOnly()
                .AnyAsync(x => x.UnitId == unitId && x.UserId == userId, cancellationToken);
            if (exists && user.UnitId == unitId)
                throw new BusinessException("Thành viên đã thuộc đơn vị này!");

            await _staffMovementService.ApplyChangeAsync(
                user,
                user.Role,
                unitId,
                changedBy,
                "Added to unit",
                DateTime.UtcNow,
                cancellationToken: cancellationToken);

            await _userRepo.SaveAsync(cancellationToken);
        }

        public Task RemoveMember(
            Guid unitId,
            Guid userId,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                async token =>
                {
                    await RemoveMemberCore(unitId, userId, changedBy, token);
                    return true;
                },
                cancellationToken);

        private async Task RemoveMemberCore(
            Guid unitId,
            Guid userId,
            Guid? changedBy,
            CancellationToken cancellationToken)
        {
            var user = await _userRepo.GetByIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException("User not found");

            var userUnit = await _userUnitRepo.QueryReadOnly()
                .FirstOrDefaultAsync(x => x.UnitId == unitId && x.UserId == userId, cancellationToken);

            var hasDirectUnit = user.UnitId == unitId;
            if (userUnit == null && !hasDirectUnit)
                throw new NotFoundException("Không tìm thấy thành viên thuộc đơn vị này!");

            if (!hasDirectUnit)
                throw new BusinessException("Dữ liệu membership không đồng bộ với User.UnitId.");

            await _staffMovementService.ApplyChangeAsync(
                user,
                user.Role,
                null,
                changedBy,
                "Removed from unit",
                DateTime.UtcNow,
                cancellationToken: cancellationToken);

            await _userRepo.SaveAsync(cancellationToken);
        }
    }
}
