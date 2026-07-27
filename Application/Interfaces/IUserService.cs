using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IUserService
    {
        Task<List<UserDto>> GetAll(CancellationToken cancellationToken = default);
        Task<List<UserDto>> GetByManager(Guid managerId, CancellationToken cancellationToken = default);
        Task<List<UserDto>> Search(string keyword, string? role, Guid? unitId, Guid? managerId = null, CancellationToken cancellationToken = default);
        Task<UserDto> Update(Guid id, UpdateUserDto dto, Guid? changedBy = null, CancellationToken cancellationToken = default);
        Task Delete(Guid id, Guid? changedBy = null, CancellationToken cancellationToken = default);
        Task<bool> CanViewPerformanceAsync(Guid requesterId, Guid targetUserId, Guid? periodId = null, CancellationToken cancellationToken = default);
        Task<PerformanceDto> GetPerformanceAsync(Guid userId, Guid? periodId = null, CancellationToken cancellationToken = default);
        Task<List<PerformanceDto>> GetUnitPerformanceAsync(Guid requesterId, Guid? periodId = null, CancellationToken cancellationToken = default);
        Task<Guid?> GetUnitIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> IsUserActive(Guid userId, CancellationToken cancellationToken = default);
    }
}
