namespace WorkManagementSystem.Domain.Entities
{
    public class KpiResult
    {
        public Guid Id { get; set; }
        public Guid PeriodId { get; set; }
        public Guid UserId { get; set; }
        public Guid? UnitId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string FullNameSnapshot { get; set; } = string.Empty;
        public string EmployeeCodeSnapshot { get; set; } = string.Empty;
        public string UnitNameSnapshot { get; set; } = string.Empty;
        public DateTime EffectiveFrom { get; set; }
        public DateTime EffectiveTo { get; set; }

        public int Score { get; set; }
        public string Level { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedOnTime { get; set; }
        public int CompletedLate { get; set; }
        public int OverdueTasks { get; set; }
        public int RejectedReports { get; set; }
        public int BonusPoints { get; set; }
        public int PenaltyPoints { get; set; }
        public int ReviewPenaltyPoints { get; set; }
        public double UnitAverageScore { get; set; }
        public int PersonalScore { get; set; }
        public bool IsManagerKpi { get; set; }
        public bool IsAtRisk { get; set; }
        public string WarningMessage { get; set; } = string.Empty;

        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LockedAt { get; set; }

        public KpiPeriod? Period { get; set; }
        public User? User { get; set; }
        public Unit? Unit { get; set; }
    }
}
