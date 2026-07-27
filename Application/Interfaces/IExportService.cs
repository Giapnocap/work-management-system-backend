namespace WorkManagementSystem.Application.Interfaces
{
    public interface IExportService
    {
        Task<byte[]> ExportTasksToExcel(Guid requestedBy, CancellationToken cancellationToken = default);
        Task<byte[]> ExportProgressToExcel(Guid requestedBy, CancellationToken cancellationToken = default);
    }
}
