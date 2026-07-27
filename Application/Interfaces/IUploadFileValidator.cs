using Microsoft.AspNetCore.Http;

namespace WorkManagementSystem.Application.Interfaces
{
    public sealed record UploadFileValidationResult(
        string Extension,
        string SafeFileName);

    public interface IUploadFileValidator
    {
        Task<UploadFileValidationResult> ValidateAsync(
            IFormFile file,
            CancellationToken cancellationToken = default);

        string GetDownloadContentType(string fileName);
    }
}
