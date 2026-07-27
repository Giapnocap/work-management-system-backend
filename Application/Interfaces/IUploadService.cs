using Microsoft.AspNetCore.Http;
using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IUploadService
    {
        Task<UploadFileDto> UploadAsync(
            IFormFile file,
            Guid? progressId,
            Guid? taskId,
            Guid uploadedBy,
            CancellationToken cancellationToken = default);

        Task<UploadFileDownloadDto?> GetFileForDownloadAsync(
            Guid id,
            Guid requestedBy,
            CancellationToken cancellationToken = default);
    }
}
