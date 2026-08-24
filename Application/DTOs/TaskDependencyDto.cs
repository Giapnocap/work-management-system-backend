using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Validation;

namespace WorkManagementSystem.Application.DTOs;

public sealed class AddTaskDependencyDto
{
    [Required]
    [NotEmptyGuid(ErrorMessage = "Cong viec tien quyet khong duoc rong.")]
    public Guid DependsOnTaskId { get; set; }
}

public sealed class TaskDependencyDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }
    public string DependsOnTaskTitle { get; set; } = string.Empty;
    public string DependsOnTaskStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}

public sealed class BlockingTaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class TaskDependencyNodeDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
}

public sealed class TaskDependencyEdgeDto
{
    public Guid TaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }
}

public sealed class TaskDependencyGraphDto
{
    public Guid RootTaskId { get; set; }
    public List<TaskDependencyNodeDto> Nodes { get; set; } = new();
    public List<TaskDependencyEdgeDto> Edges { get; set; } = new();
}
