using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Domain.Entities;

public sealed class ReminderPolicy : IHasRowVersion
{
    public Guid Id { get; set; }
    public ReminderPolicyScope ScopeType { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? ProjectId { get; set; }
    public int BeforeDueHours { get; set; } = 24;
    public int OverdueEscalationHours { get; set; } = 24;
    public bool NotifyAssignee { get; set; } = true;
    public bool NotifyManager { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Unit? Unit { get; set; }
    public Project? Project { get; set; }
}
