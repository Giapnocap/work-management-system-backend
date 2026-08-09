using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IUnitService
    {
        Task<List<UnitDto>> GetAll(CancellationToken cancellationToken = default);
        Task<UnitDto> GetMyUnit(Guid userId, CancellationToken cancellationToken = default);
        Task<List<UserDto>> GetUsers(Guid unitId, CancellationToken cancellationToken = default);
        Task<List<UserDto>> GetVisibleUsers(Guid unitId, Guid requesterId, CancellationToken cancellationToken = default);
        Task<UnitDto> Create(CreateUnitDto dto, Guid? changedBy = null, CancellationToken cancellationToken = default);
        Task<UnitDto> Update(Guid id, UpdateUnitDto dto, Guid? changedBy = null, CancellationToken cancellationToken = default);
        Task Delete(Guid id, Guid? changedBy = null, CancellationToken cancellationToken = default);
        Task AddMember(
            Guid unitId,
            Guid userId,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default);
        Task RemoveMember(
            Guid unitId,
            Guid userId,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default);
        Task AddMemberForRequester(Guid unitId, Guid userId, Guid requesterId, CancellationToken cancellationToken = default);
        Task RemoveMemberForRequester(Guid unitId, Guid userId, Guid requesterId, CancellationToken cancellationToken = default);
    }
}
