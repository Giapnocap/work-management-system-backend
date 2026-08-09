using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Storage;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class UploadOrphanCleanupTests
{
    [Fact]
    public async Task DeleteOrphans_UsesPersistedKeysAndMinimumAge()
    {
        await using var context = TestFactory.CreateDbContext();
        using var contentRoot = new TempContentRoot();
        var uploadsRoot = Path.Combine(contentRoot.Environment.ContentRootPath, "Uploads");
        Directory.CreateDirectory(uploadsRoot);

        const string referencedKey = "referenced.pdf";
        const string oldOrphanKey = "old-orphan.pdf";
        const string recentOrphanKey = "recent-orphan.pdf";
        context.UploadFiles.Add(new UploadFile
        {
            Id = Guid.NewGuid(),
            FileName = referencedKey,
            StorageKey = referencedKey,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            TaskId = Guid.NewGuid()
        });
        await context.SaveChangesAsync();

        var referencedPath = Path.Combine(uploadsRoot, referencedKey);
        var oldOrphanPath = Path.Combine(uploadsRoot, oldOrphanKey);
        var recentOrphanPath = Path.Combine(uploadsRoot, recentOrphanKey);
        await File.WriteAllTextAsync(referencedPath, "kept");
        await File.WriteAllTextAsync(oldOrphanPath, "delete");
        await File.WriteAllTextAsync(recentOrphanPath, "recent");
        File.SetLastWriteTimeUtc(referencedPath, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(oldOrphanPath, DateTime.UtcNow.AddDays(-2));

        var cleaner = new UploadOrphanCleaner(
            context,
            contentRoot.Environment,
            Options.Create(new UploadCleanupOptions
            {
                MinimumAgeHours = 24,
                IntervalHours = 24
            }),
            NullLogger<UploadOrphanCleaner>.Instance);

        var deletedCount = await cleaner.DeleteOrphansAsync();

        Assert.Equal(1, deletedCount);
        Assert.True(File.Exists(referencedPath));
        Assert.False(File.Exists(oldOrphanPath));
        Assert.True(File.Exists(recentOrphanPath));
    }

    private sealed class TempContentRoot : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"wms-cleanup-tests-{Guid.NewGuid():N}");

        public TempContentRoot()
        {
            Directory.CreateDirectory(_path);
            Environment = new TestWebHostEnvironment(_path);
        }

        public IWebHostEnvironment Environment { get; }

        public void Dispose()
        {
            if (Directory.Exists(_path))
                Directory.Delete(_path, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string ApplicationName { get; set; } = "WorkManagementSystem.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
