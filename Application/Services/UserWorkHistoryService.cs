using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Services
{
    public class UserWorkHistoryService : IUserWorkHistoryService
    {
        private readonly IAppDbContext _context;

        public UserWorkHistoryService(IAppDbContext context)
        {
            _context = context;
        }

        public async Task RecordChangeAsync(
            User user,
            Guid? newUnitId,
            string newRole,
            Guid? changedBy,
            string reason,
            DateTime changedAt,
            CancellationToken cancellationToken = default)
        {
            var unitChanged = user.UnitId != newUnitId;
            var roleChanged = user.Role != newRole;

            var activeHistories = await _context.UserWorkHistories
                .IgnoreQueryFilters()
                .Where(h => h.UserId == user.Id && h.EffectiveTo == null)
                .OrderByDescending(h => h.EffectiveFrom)
                .Take(2)
                .ToListAsync(cancellationToken);

            if (activeHistories.Count > 1)
                throw new BusinessException("Lich su nhan su khong hop le: co nhieu hon mot giai doan dang mo.");

            var activeHistory = activeHistories.SingleOrDefault();

            if (activeHistory == null)
            {
                var start = user.JoinedUnitAt == default ? changedAt : user.JoinedUnitAt;
                if (start > changedAt) start = changedAt;

                activeHistory = new UserWorkHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    UnitId = user.UnitId,
                    Role = user.Role,
                    EffectiveFrom = start,
                    ChangedBy = changedBy,
                    ChangeReason = "Initial history"
                };

                _context.UserWorkHistories.Add(activeHistory);
            }

            if (!unitChanged && !roleChanged)
                return;

            activeHistory.EffectiveTo = changedAt > activeHistory.EffectiveFrom
                ? changedAt.AddTicks(-1)
                : changedAt;

            _context.UserWorkHistories.Add(new UserWorkHistory
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                UnitId = newUnitId,
                Role = newRole,
                EffectiveFrom = changedAt,
                ChangedBy = changedBy,
                ChangeReason = reason
            });
        }

        public async Task CloseCurrentAsync(
            User user,
            Guid? changedBy,
            DateTime changedAt,
            CancellationToken cancellationToken = default)
        {
            var activeHistories = await _context.UserWorkHistories
                .IgnoreQueryFilters()
                .Where(history => history.UserId == user.Id && history.EffectiveTo == null)
                .OrderByDescending(history => history.EffectiveFrom)
                .Take(2)
                .ToListAsync(cancellationToken);

            if (activeHistories.Count > 1)
                throw new BusinessException("Lich su nhan su khong hop le: co nhieu hon mot giai doan dang mo.");

            var activeHistory = activeHistories.SingleOrDefault();
            if (activeHistory == null)
            {
                var effectiveFrom = user.JoinedUnitAt == default ? changedAt : user.JoinedUnitAt;
                if (effectiveFrom > changedAt)
                    effectiveFrom = changedAt;

                activeHistory = new UserWorkHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    UnitId = user.UnitId,
                    Role = user.Role,
                    EffectiveFrom = effectiveFrom,
                    ChangedBy = changedBy,
                    ChangeReason = "Backfilled before account deactivation"
                };
                _context.UserWorkHistories.Add(activeHistory);
            }

            activeHistory.EffectiveTo = changedAt < activeHistory.EffectiveFrom
                ? activeHistory.EffectiveFrom
                : changedAt;
        }
    }
}
