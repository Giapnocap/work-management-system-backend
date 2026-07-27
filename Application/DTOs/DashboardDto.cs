namespace WorkManagementSystem.Application.DTOs
{
    public class DashboardDto
    {
        public int TotalTasks { get; set; }
        public int TotalUsers { get; set; }
        public int TotalUnits { get; set; }
        public int TaskPending { get; set; }
        public int TaskInProgress { get; set; }
        public int TaskApproved { get; set; }
        public int RejectedReports { get; set; }
        public int ReportSubmitted { get; set; }
        public List<UnitSummaryDto> UnitSummaries { get; set; } = new();
    }

    public class UnitSummaryDto
    {
        public string UnitName { get; set; } = string.Empty;
        public string UnitCode { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int ApprovedTasks { get; set; }
    }

    public class ManagerDashboardDto
    {
        public string UnitName { get; set; } = string.Empty;
        public int TotalMembers { get; set; }
        public int TotalTasks { get; set; }
        public int TaskPending { get; set; }
        public int TaskInProgress { get; set; }
        public int TaskApproved { get; set; }
        public int RejectedReports { get; set; }
        public int ReportSubmitted { get; set; }
        public List<MemberProgressDto> MemberProgresses { get; set; } = new();
    }

    public class MemberProgressDto
    {
        public string FullName { get; set; } = string.Empty;
        public string UserEmployeeCode { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int ApprovedTasks { get; set; }
        public int SubmittedTasks { get; set; }
    }
}
