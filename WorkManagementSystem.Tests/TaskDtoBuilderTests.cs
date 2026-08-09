using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Tests.TestSupport;
using TaskItem = WorkManagementSystem.Domain.Entities.TaskItem;

namespace WorkManagementSystem.Tests;

public class TaskDtoBuilderTests
{
    [Fact]
    public async Task BuildTaskDtos_GroupsRelatedDataByTask()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Engineering" };
        var creator = CreateUser("manager", "Manager", unit.Id);
        var firstAssignee = CreateUser("employee-one", "User", unit.Id);
        var secondAssignee = CreateUser("employee-two", "User", unit.Id);
        var firstTask = CreateTask("First task", creator.Id, unit.Id);
        var secondTask = CreateTask("Second task", creator.Id, unit.Id);

        context.AddRange(unit, creator, firstAssignee, secondAssignee, firstTask, secondTask);
        context.TaskAssignees.AddRange(
            new TaskAssignee
            {
                Id = Guid.NewGuid(),
                TaskId = firstTask.Id,
                UserId = firstAssignee.Id
            },
            new TaskAssignee
            {
                Id = Guid.NewGuid(),
                TaskId = secondTask.Id,
                UserId = secondAssignee.Id
            });
        context.UploadFiles.AddRange(
            CreateUpload(firstTask.Id, firstAssignee.Id, "first.txt"),
            CreateUpload(secondTask.Id, secondAssignee.Id, "second.txt"));
        context.SubTasks.AddRange(
            CreateSubTask(firstTask.Id, "First step"),
            CreateSubTask(secondTask.Id, "Second step"));
        await context.SaveChangesAsync();

        var builder = TestFactory.CreateTaskDtoBuilder(context);
        var result = await builder.BuildTaskDtos(new[] { firstTask, secondTask });

        var firstDto = Assert.Single(result, dto => dto.Id == firstTask.Id);
        Assert.Equal(firstAssignee.Id, Assert.Single(firstDto.Assignees).Id);
        Assert.Equal("first.txt", Assert.Single(firstDto.Files).FileName);
        Assert.Equal("First step", Assert.Single(firstDto.SubTasks).Title);

        var secondDto = Assert.Single(result, dto => dto.Id == secondTask.Id);
        Assert.Equal(secondAssignee.Id, Assert.Single(secondDto.Assignees).Id);
        Assert.Equal("second.txt", Assert.Single(secondDto.Files).FileName);
        Assert.Equal("Second step", Assert.Single(secondDto.SubTasks).Title);
    }

    private static User CreateUser(string username, string role, Guid unitId)
        => new()
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = username,
            EmployeeCode = username.ToUpperInvariant(),
            PasswordHash = "hash",
            Role = role,
            UnitId = unitId,
            IsApproved = true
        };

    private static TaskItem CreateTask(string title, Guid creatorId, Guid unitId)
        => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedBy = creatorId,
            CreatedAt = DateTime.UtcNow,
            UnitId = unitId
        };

    private static UploadFile CreateUpload(Guid taskId, Guid uploadedBy, string fileName)
        => new()
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            UploadedBy = uploadedBy,
            FileName = fileName,
            StorageKey = fileName,
            CreatedAt = DateTime.UtcNow
        };

    private static SubTask CreateSubTask(Guid taskId, string title)
        => new()
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Title = title,
            CreatedAt = DateTime.UtcNow
        };
}
