using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardDto> GetDashboard(CancellationToken cancellationToken = default);
        Task<ManagerDashboardDto> GetManagerDashboard(Guid userId, CancellationToken cancellationToken = default);
    }
}
