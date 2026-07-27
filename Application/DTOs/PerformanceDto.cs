namespace WorkManagementSystem.Application.DTOs
{
    public class PerformanceDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public Guid? PeriodId { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public string PeriodStatus { get; set; } = "Open";
        public Guid? UnitId { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsLocked { get; set; }
        public bool IsPartialPeriod { get; set; }
        public string PeriodNote { get; set; } = string.Empty;

        public int Score { get; set; }
        public string Level { get; set; } = string.Empty;
        public string LevelColor { get; set; } = string.Empty;
        public string LevelIcon { get; set; } = string.Empty;

        public int TotalTasks { get; set; }
        public int CompletedOnTime { get; set; }
        public int CompletedLate { get; set; }
        public int OverdueTasks { get; set; }
        public int RejectedReports { get; set; }

        public int BonusPoints { get; set; }
        public int PenaltyPoints { get; set; }
        public int ReviewPenaltyPoints { get; set; }

        public bool IsManagerKpi { get; set; }
        public double UnitAverageScore { get; set; }
        public int PersonalScore { get; set; }

        public bool IsAtRisk { get; set; }
        public string WarningMessage { get; set; } = string.Empty;
    }
}
