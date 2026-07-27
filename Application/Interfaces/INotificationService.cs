using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface INotificationService
    {
        Task AddNotification(Guid userId, string message, CancellationToken cancellationToken = default);
        Task<List<NotificationDto>> GetMyNotifications(Guid userId, CancellationToken cancellationToken = default);
        Task MarkAsRead(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);
        Task<int> GetUnreadCount(Guid userId, CancellationToken cancellationToken = default);
    }
}
