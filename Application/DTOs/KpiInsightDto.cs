namespace WorkManagementSystem.Application.DTOs;

public sealed class KpiDashboardDto
{
    public KpiPeriodDto Period { get; set; } = new();
    public string Scope { get; set; } = string.Empty;
    public Guid? ScopeUnitId { get; set; }
    public string ScopeUnitName { get; set; } = string.Empty;
    public KpiInsightSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<KpiUnitInsightDto> Units { get; set; } = Array.Empty<KpiUnitInsightDto>();
    public IReadOnlyList<PerformanceDto> Users { get; set; } = Array.Empty<PerformanceDto>();
    public KpiFormulaDto Formula { get; set; } = new();
    public string UsageNotice { get; set; } = string.Empty;
}

public sealed class KpiUnitInsightDto
{
    public Guid? UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public KpiInsightSummaryDto Summary { get; set; } = new();
}

public sealed class KpiInsightSummaryDto
{
    public int UserCount { get; set; }
    public int AtRiskUserCount { get; set; }
    public decimal AverageScore { get; set; }
    public int TotalTasks { get; set; }
    public int Throughput { get; set; }
    public int OverdueTasks { get; set; }
    public int RejectedReports { get; set; }
    public int ProgressReportCount { get; set; }
    public decimal PlannedEffortHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal OverdueRate { get; set; }
    public decimal ReviewRejectionRate { get; set; }
    public decimal? EstimationAccuracy { get; set; }
}

public sealed class KpiFormulaDto
{
    public string Version { get; set; } = string.Empty;
    public int PersonalBaseScore { get; set; }
    public int PersonalMaximumScore { get; set; }
    public int OnTimeCompletionBonus { get; set; }
    public int NoDeadlineCompletionBonus { get; set; }
    public int RejectedReportPenalty { get; set; }
    public int FirstOverduePenalty { get; set; }
    public int SecondOverduePenalty { get; set; }
    public int LaterOverduePenalty { get; set; }
    public int ManagerUnitWeightPercent { get; set; }
    public int ManagerPersonalWeightPercent { get; set; }
    public bool UsedForAutomaticHrDecisions { get; set; }
}
