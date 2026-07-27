using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;

namespace WorkManagementSystem.Application.Services
{
    public class UserUnitMembershipService : IUserUnitMembershipService
    {
        private readonly IGenericRepository<UserUnit> _userUnitRepo;

        public UserUnitMembershipService(IGenericRepository<UserUnit> userUnitRepo)
        {
            _userUnitRepo = userUnitRepo;
        }

        public async Task ReplaceMembership(
            Guid userId,
            Guid? unitId,
            CancellationToken cancellationToken = default)
        {
            var oldMappings = await _userUnitRepo.Query()
                .Where(uu => uu.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var mapping in oldMappings)
                _userUnitRepo.Delete(mapping);

            if (unitId.HasValue)
            {
                await _userUnitRepo.AddAsync(new UserUnit
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    UnitId = unitId.Value
                }, cancellationToken);
            }
        }
    }
}
