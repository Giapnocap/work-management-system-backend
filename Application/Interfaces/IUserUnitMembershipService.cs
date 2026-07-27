namespace WorkManagementSystem.Application.Interfaces
{
    public interface IUserUnitMembershipService
    {
        Task ReplaceMembership(
            Guid userId,
            Guid? unitId,
            CancellationToken cancellationToken = default);
    }
}
