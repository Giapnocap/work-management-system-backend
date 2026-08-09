using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using System.IO.Compression;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public class UploadServiceTests
{
    [Fact]
    public async Task Upload_ValidPng_StoresFile_WithoutExposingPhysicalPath()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var service = CreateService(tempRoot, context);

        var result = await service.UploadAsync(
            CreateFile("proof.png", "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 }),
            progressId: null,
            taskId: task.Id,
            uploadedBy: user.Id);

        var saved = context.UploadFiles.Single();
        Assert.Equal("proof.png", result.FileName);
        Assert.Equal(task.Id, saved.TaskId);
        Assert.False(Path.IsPathRooted(saved.StorageKey));
        Assert.Equal(saved.StorageKey, Path.GetFileName(saved.StorageKey));
        Assert.True(File.Exists(Path.Combine(
            tempRoot.Environment.ContentRootPath,
            "Uploads",
            saved.StorageKey)));
        Assert.Null(typeof(UploadFileDto).GetProperty("FilePath"));
        Assert.Null(typeof(UploadFileDto).GetProperty("StorageKey"));
    }

    [Fact]
    public async Task Upload_WithoutTaskOrProgress_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, _) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var service = CreateService(tempRoot, context);

        await Assert.ThrowsAsync<BusinessException>(() => service.UploadAsync(
            CreateFile("proof.png", "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            progressId: null,
            taskId: null,
            uploadedBy: user.Id));

        Assert.Empty(context.UploadFiles);
    }

    [Fact]
    public async Task Upload_WithMismatchedTaskAndProgress_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        var otherTask = CreateTask("Other task", task.CreatedBy, task.UnitId!.Value);
        var progress = new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = user.Id,
            Percent = 50,
            UpdatedAt = DateTime.UtcNow
        };
        context.Tasks.Add(otherTask);
        context.Progresses.Add(progress);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = otherTask.Id,
            UserId = user.Id
        });
        await context.SaveChangesAsync();
        using var tempRoot = new TempContentRoot();
        var service = CreateService(tempRoot, context);

        await Assert.ThrowsAsync<BusinessException>(() => service.UploadAsync(
            CreateFile("proof.png", "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            progressId: progress.Id,
            taskId: otherTask.Id,
            uploadedBy: user.Id));

        Assert.Empty(context.UploadFiles);
    }

    [Fact]
    public async Task Upload_WithProgressOnly_UsesProgressTask()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        var progress = new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = user.Id,
            Percent = 50,
            UpdatedAt = DateTime.UtcNow
        };
        context.Progresses.Add(progress);
        await context.SaveChangesAsync();
        using var tempRoot = new TempContentRoot();
        var service = CreateService(tempRoot, context);

        var result = await service.UploadAsync(
            CreateFile("proof.png", "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            progressId: progress.Id,
            taskId: null,
            uploadedBy: user.Id);

        Assert.Equal(task.Id, result.TaskId);
        Assert.Equal(progress.Id, result.ProgressId);
    }

    [Fact]
    public async Task Upload_DisallowedArchive_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var service = CreateService(tempRoot, context);

        await Assert.ThrowsAsync<BusinessException>(() => service.UploadAsync(
            CreateFile("payload.zip", "application/zip", new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
            progressId: null,
            taskId: task.Id,
            uploadedBy: user.Id));
    }

    [Fact]
    public async Task Upload_MismatchedContentType_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var service = CreateService(tempRoot, context);

        await Assert.ThrowsAsync<BusinessException>(() => service.UploadAsync(
            CreateFile("proof.png", "application/pdf", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            progressId: null,
            taskId: task.Id,
            uploadedBy: user.Id));
    }

    [Fact]
    public async Task Upload_MismatchedSignature_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var service = CreateService(tempRoot, context);

        await Assert.ThrowsAsync<BusinessException>(() => service.UploadAsync(
            CreateFile("proof.pdf", "application/pdf", "this is not a pdf"u8.ToArray()),
            progressId: null,
            taskId: task.Id,
            uploadedBy: user.Id));
    }

    [Fact]
    public async Task GetFileForDownload_ReturnsInternalPathAndContentType()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var uploadsRoot = Path.Combine(tempRoot.Environment.ContentRootPath, "Uploads");
        Directory.CreateDirectory(uploadsRoot);
        var storedPath = Path.Combine(uploadsRoot, "proof.pdf");
        await File.WriteAllBytesAsync(storedPath, "%PDF-test"u8.ToArray());
        var upload = new UploadFile
        {
            Id = Guid.NewGuid(),
            FileName = "proof.pdf",
            StorageKey = "proof.pdf",
            CreatedAt = DateTime.UtcNow,
            TaskId = task.Id,
            UploadedBy = user.Id
        };
        context.UploadFiles.Add(upload);
        await context.SaveChangesAsync();
        var service = CreateService(tempRoot, context);

        var result = await service.GetFileForDownloadAsync(upload.Id, user.Id);

        Assert.Equal(storedPath, result.PhysicalPath);
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task GetFileForDownload_MissingPhysicalFile_ThrowsNotFoundException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var uploadsRoot = Path.Combine(tempRoot.Environment.ContentRootPath, "Uploads");
        Directory.CreateDirectory(uploadsRoot);
        var upload = new UploadFile
        {
            Id = Guid.NewGuid(),
            FileName = "missing.pdf",
            StorageKey = "missing.pdf",
            CreatedAt = DateTime.UtcNow,
            TaskId = task.Id,
            UploadedBy = user.Id
        };
        context.UploadFiles.Add(upload);
        await context.SaveChangesAsync();
        var service = CreateService(tempRoot, context);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetFileForDownloadAsync(upload.Id, user.Id));
    }

    [Fact]
    public async Task Upload_DisguisedZipAsDocx_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var service = CreateService(tempRoot, context);

        await Assert.ThrowsAsync<BusinessException>(() => service.UploadAsync(
            CreateFile(
                "proof.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                CreateZip(("payload.txt", "not a Word document"))),
            progressId: null,
            taskId: task.Id,
            uploadedBy: user.Id));

        Assert.Empty(context.UploadFiles);
    }

    [Fact]
    public async Task Upload_DocxContainingMacro_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var service = CreateService(tempRoot, context);
        var fileBytes = CreateZip(
            ("[Content_Types].xml", "<Types />"),
            ("_rels/.rels", "<Relationships />"),
            ("word/document.xml", "<document />"),
            ("word/vbaProject.bin", "macro"));

        await Assert.ThrowsAsync<BusinessException>(() => service.UploadAsync(
            CreateFile(
                "proof.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileBytes),
            progressId: null,
            taskId: task.Id,
            uploadedBy: user.Id));

        Assert.Empty(context.UploadFiles);
    }

    [Fact]
    public async Task Upload_ValidDocx_StoresSanitizedOriginalName()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var service = CreateService(tempRoot, context);
        var fileBytes = CreateZip(
            ("[Content_Types].xml", "<Types />"),
            ("_rels/.rels", "<Relationships />"),
            ("word/document.xml", "<document />"));

        var result = await service.UploadAsync(
            CreateFile(
                "../unsafe\r\nname.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileBytes),
            progressId: null,
            taskId: task.Id,
            uploadedBy: user.Id);

        Assert.Equal("unsafename.docx", result.FileName);
        Assert.Equal("unsafename.docx", context.UploadFiles.Single().FileName);
    }

    [Fact]
    public async Task GetFileForDownload_RootedStorageKey_ThrowsNotFoundException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var upload = new UploadFile
        {
            Id = Guid.NewGuid(),
            FileName = "outside.pdf",
            StorageKey = Path.Combine(tempRoot.Environment.ContentRootPath, "outside.pdf"),
            CreatedAt = DateTime.UtcNow,
            TaskId = task.Id,
            UploadedBy = user.Id
        };
        context.UploadFiles.Add(upload);
        await context.SaveChangesAsync();
        var service = CreateService(tempRoot, context);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetFileForDownloadAsync(upload.Id, user.Id));
    }

    [Fact]
    public async Task GetFileForDownload_TraversalStorageKey_ThrowsNotFoundException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (user, task) = await SeedUploadTask(context);
        using var tempRoot = new TempContentRoot();
        var upload = new UploadFile
        {
            Id = Guid.NewGuid(),
            FileName = "outside.pdf",
            StorageKey = "../outside.pdf",
            CreatedAt = DateTime.UtcNow,
            TaskId = task.Id,
            UploadedBy = user.Id
        };
        context.UploadFiles.Add(upload);
        await context.SaveChangesAsync();
        var service = CreateService(tempRoot, context);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetFileForDownloadAsync(upload.Id, user.Id));
    }

    private static async Task<(User User, TaskItem Task)> SeedUploadTask(AppDbContext context)
    {
        var unit = new Unit { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString("N") };
        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = Guid.NewGuid().ToString("N"),
            FullName = "Upload Manager",
            EmployeeCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            PasswordHash = "hash",
            Role = "Manager",
            UnitId = unit.Id,
            IsApproved = true
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = Guid.NewGuid().ToString("N"),
            FullName = "Upload User",
            EmployeeCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            PasswordHash = "hash",
            Role = "User",
            UnitId = unit.Id,
            IsApproved = true
        };
        var task = CreateTask("Upload task", manager.Id, unit.Id);
        context.Units.Add(unit);
        context.Users.AddRange(manager, user);
        context.Tasks.Add(task);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = user.Id
        });
        await context.SaveChangesAsync();
        return (user, task);
    }

    private static TaskItem CreateTask(string title, Guid managerId, Guid unitId)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = string.Empty,
            CreatedBy = managerId,
            UnitId = unitId,
            Status = TaskStatusEnum.InProgress,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static IFormFile CreateFile(string fileName, string contentType, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static UploadService CreateService(TempContentRoot tempRoot, AppDbContext context)
        => new(
            tempRoot.Environment,
            context,
            new TaskAccessService(context),
            new UploadFileValidator());

    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private sealed class TempContentRoot : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"wms-tests-{Guid.NewGuid():N}");

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
