using System.ComponentModel.DataAnnotations;

namespace WorkManagementSystem.Application.DTOs;

public abstract class RecurringTaskDefinitionDto
{
    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [MaxLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string Description { get; set; } = string.Empty;

    [RegularExpression("^(Low|Medium|High|Urgent)$", ErrorMessage = "Mức ưu tiên không hợp lệ.")]
    public string Priority { get; set; } = "Medium";

    public bool RequiresReview { get; set; } = true;

    [Range(0.01, 100000.0, ErrorMessage = "Khối lượng kế hoạch phải lớn hơn 0.")]
    public decimal? PlannedEffortHours { get; set; }

    [Required(ErrorMessage = "Loại lịch lặp không được để trống.")]
    [RegularExpression("^(Daily|Weekly|Monthly)$", ErrorMessage = "Loại lịch lặp không hợp lệ.")]
    public string RecurrenceType { get; set; } = string.Empty;

    [Range(1, 365, ErrorMessage = "Khoảng lặp phải từ 1 đến 365.")]
    public int Interval { get; set; } = 1;

    [Range(0, 6, ErrorMessage = "Thứ trong tuần phải từ 0 (Chủ nhật) đến 6 (Thứ bảy).")]
    public int? DayOfWeek { get; set; }

    [Range(1, 31, ErrorMessage = "Ngày trong tháng phải từ 1 đến 31.")]
    public int? DayOfMonth { get; set; }

    public DateTimeOffset NextRunAtUtc { get; set; }
    public Guid? ProjectId { get; set; }
    public List<Guid> UserIds { get; set; } = new();
}

public sealed class CreateRecurringTaskDto : RecurringTaskDefinitionDto
{
}

public sealed class UpdateRecurringTaskDto : RecurringTaskDefinitionDto
{
    [Required(ErrorMessage = "RowVersion không được để trống.")]
    [MinLength(1, ErrorMessage = "RowVersion không hợp lệ.")]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class RecurringTaskAssigneeDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
}

public sealed class RecurringTaskTemplateDto
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public bool RequiresReview { get; set; }
    public decimal? PlannedEffortHours { get; set; }
    public string RecurrenceType { get; set; } = string.Empty;
    public int Interval { get; set; }
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public DateTime NextRunAtUtc { get; set; }
    public DateTime? LastGeneratedAtUtc { get; set; }
    public bool IsActive { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<RecurringTaskAssigneeDto> Assignees { get; set; } = new();
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
