using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Services
{
    public class UploadService : IUploadService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IAppDbContext _context;
        private readonly ITaskAccessService _accessService;
        private readonly IUploadFileValidator _fileValidator;

        public UploadService(
            IWebHostEnvironment env,
            IAppDbContext context,
            ITaskAccessService accessService,
            IUploadFileValidator fileValidator)
        {
            _env = env;
            _context = context;
            _accessService = accessService;
            _fileValidator = fileValidator;
        }

        public async Task<UploadFileDto> UploadAsync(
            IFormFile file,
            Guid? progressId,
            Guid? taskId,
            Guid uploadedBy,
            CancellationToken cancellationToken = default)
        {
            var resolvedTaskId = await ResolveTaskId(
                progressId,
                taskId,
                cancellationToken);

            if (!await _accessService.CanAccessTask(
                    resolvedTaskId,
                    uploadedBy,
                    cancellationToken: cancellationToken))
                throw new ForbiddenException("Bạn không có quyền tải tệp lên công việc này.");

            var validation = await _fileValidator.ValidateAsync(file, cancellationToken);

            var folderPath = GetUploadsRoot();
            Directory.CreateDirectory(folderPath);

            var storageKey = $"{Guid.NewGuid():N}{validation.Extension}";
            var filePath = ResolveStoragePath(storageKey);

            try
            {
                await using (var stream = new FileStream(filePath, FileMode.CreateNew))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                var upload = new UploadFile
                {
                    Id = Guid.NewGuid(),
                    FileName = validation.SafeFileName,
                    StorageKey = storageKey,
                    CreatedAt = DateTime.UtcNow,
                    ProgressId = progressId,
                    TaskId = resolvedTaskId,
                    UploadedBy = uploadedBy
                };

                _context.UploadFiles.Add(upload);
                await _context.SaveChangesAsync(cancellationToken);

                return MapFile(upload);
            }
            catch
            {
                DeleteFileIfExists(filePath);
                throw;
            }
        }

        public async Task<UploadFileDownloadDto> GetFileForDownloadAsync(
            Guid id,
            Guid requestedBy,
            CancellationToken cancellationToken = default)
        {
            var upload = await _context.UploadFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(file => file.Id == id, cancellationToken);
            if (upload == null)
                throw new NotFoundException("Không tìm thấy tệp.");

            if (!await _accessService.CanAccessUpload(id, requestedBy, cancellationToken))
                throw new ForbiddenException("Bạn không có quyền tải tệp này.");

            var filePath = ResolveStoragePath(upload.StorageKey);
            if (!File.Exists(filePath))
                throw new NotFoundException("Không tìm thấy nội dung vật lý của tệp.");

            return new UploadFileDownloadDto
            {
                Id = upload.Id,
                FileName = upload.FileName,
                PhysicalPath = filePath,
                ContentType = _fileValidator.GetDownloadContentType(upload.FileName)
            };
        }

        private static UploadFileDto MapFile(UploadFile upload)
        {
            return new UploadFileDto
            {
                Id = upload.Id,
                FileName = upload.FileName,
                CreatedAt = upload.CreatedAt,
                ProgressId = upload.ProgressId,
                TaskId = upload.TaskId,
                UploadedBy = upload.UploadedBy
            };
        }

        private async Task<Guid> ResolveTaskId(
            Guid? progressId,
            Guid? taskId,
            CancellationToken cancellationToken)
        {
            if (!progressId.HasValue && !taskId.HasValue)
                throw new BusinessException("Tệp phải được gắn với một công việc hoặc báo cáo.");

            if (!progressId.HasValue)
                return taskId!.Value;

            var progressTaskId = await _context.Progresses
                .AsNoTracking()
                .Where(progress => progress.Id == progressId.Value)
                .Select(progress => (Guid?)progress.TaskId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy báo cáo tiến độ.");

            if (taskId.HasValue && taskId.Value != progressTaskId)
                throw new BusinessException("Công việc không khớp với báo cáo đã chọn.");

            return progressTaskId;
        }

        private string GetUploadsRoot()
            => Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Uploads"));

        private string ResolveStoragePath(string storageKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(storageKey) ||
                    Path.IsPathRooted(storageKey) ||
                    !string.Equals(storageKey, Path.GetFileName(storageKey), StringComparison.Ordinal))
                {
                    throw new NotFoundException("Không tìm thấy tệp.");
                }

                var uploadsRoot = GetUploadsRoot();
                var uploadsPrefix = uploadsRoot.EndsWith(Path.DirectorySeparatorChar)
                    ? uploadsRoot
                    : $"{uploadsRoot}{Path.DirectorySeparatorChar}";
                var resolvedPath = Path.GetFullPath(Path.Combine(uploadsRoot, storageKey));
                var comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                if (resolvedPath.StartsWith(uploadsPrefix, comparison))
                    return resolvedPath;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                    or NotSupportedException
                    or PathTooLongException)
            {
            }

            throw new NotFoundException("Không tìm thấy tệp.");
        }

        private static void DeleteFileIfExists(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
            }
        }
    }
}
