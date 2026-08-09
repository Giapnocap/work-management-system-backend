using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.Infrastructure.Storage;

public sealed class UploadOrphanCleaner
{
    private readonly IAppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly UploadCleanupOptions _options;
    private readonly ILogger<UploadOrphanCleaner> _logger;

    public UploadOrphanCleaner(
        IAppDbContext context,
        IWebHostEnvironment environment,
        IOptions<UploadCleanupOptions> options,
        ILogger<UploadOrphanCleaner> logger)
    {
        _context = context;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> DeleteOrphansAsync(CancellationToken cancellationToken = default)
    {
        var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "Uploads"));
        if (!Directory.Exists(uploadsRoot))
            return 0;

        var storedKeys = await _context.UploadFiles
            .AsNoTracking()
            .Select(file => file.StorageKey)
            .ToListAsync(cancellationToken);
        var referencedKeys = storedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cutoff = DateTime.UtcNow.AddHours(-_options.MinimumAgeHours);
        var deletedCount = 0;
        foreach (var filePath in Directory.EnumerateFiles(uploadsRoot, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var storageKey = Path.GetFileName(filePath);
            if (referencedKeys.Contains(storageKey) || File.GetLastWriteTimeUtc(filePath) > cutoff)
                continue;

            try
            {
                File.Delete(filePath);
                deletedCount++;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(
                    exception,
                    "Could not delete orphan upload {StorageKey}.",
                    storageKey);
            }
        }

        return deletedCount;
    }
}
