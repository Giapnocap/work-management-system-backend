using System.ComponentModel.DataAnnotations;

namespace WorkManagementSystem.Application.DTOs;

public sealed class UpdateUserCapacityDto
{
    [Range(0.01, 168.0, ErrorMessage = "Sức chứa tuần phải lớn hơn 0 và không vượt quá 168 giờ.")]
    public decimal WeeklyCapacityHours { get; set; }

    public DateTime? EffectiveFrom { get; set; }
}

public sealed class UserCapacityDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal WeeklyCapacityHours { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public sealed class WorkloadSummaryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public Guid? UnitId { get; set; }
    public decimal BusyThresholdPercent { get; set; }
    public decimal OverloadedThresholdPercent { get; set; }
    public int UserCount { get; set; }
    public int BusyUserCount { get; set; }
    public int OverloadedUserCount { get; set; }
    public List<UserWorkloadDto> Users { get; set; } = new();
}

public sealed class UserWorkloadDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public Guid UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal CapacityHours { get; set; }
    public decimal RemainingWorkHours { get; set; }
    public decimal WorkloadPercent { get; set; }
    public string Level { get; set; } = string.Empty;
    public int ActiveTaskCount { get; set; }
    public int OverdueTaskCount { get; set; }
}

public sealed class AssignmentPreviewRequestDto
{
    [Range(0.01, 100000.0, ErrorMessage = "Khối lượng kế hoạch phải lớn hơn 0.")]
    public decimal PlannedEffortHours { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public List<Guid> UserIds { get; set; } = new();
    public List<Guid> UnitIds { get; set; } = new();
}

public sealed class AssignmentPreviewDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal PlannedEffortHours { get; set; }
    public decimal EffortPerAssigneeHours { get; set; }
    public bool HasWarning { get; set; }
    public List<AssignmentWorkloadDto> Assignees { get; set; } = new();
}

public sealed class AssignmentWorkloadDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public decimal CurrentWorkloadHours { get; set; }
    public decimal ProjectedWorkloadHours { get; set; }
    public decimal CapacityHours { get; set; }
    public decimal CurrentWorkloadPercent { get; set; }
    public decimal ProjectedWorkloadPercent { get; set; }
    public string ProjectedLevel { get; set; } = string.Empty;
    public bool HasWarning { get; set; }
}
