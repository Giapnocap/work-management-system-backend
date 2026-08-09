namespace WorkManagementSystem.Application.Interfaces;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    string? Role { get; }
    Guid GetRequiredUserId();
}
