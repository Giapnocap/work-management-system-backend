namespace WorkManagementSystem.Application.Common;

public static class KpiFormula
{
    public const string Version = "1.0";

    public const int BaseScore = 100;
    public const int MinimumScore = 0;
    public const int MaximumPersonalScore = 120;

    public const int OnTimeCompletionBonus = 5;
    public const int NoDeadlineCompletionBonus = 3;
    public const int ThreeTaskStreakBonus = 2;
    public const int FiveTaskStreakBonus = 5;
    public const int RejectedReportPenalty = 3;
    public const int FirstOverduePenalty = 5;
    public const int SecondOverduePenalty = 8;
    public const int LaterOverduePenalty = 12;

    public const double ManagerUnitWeight = 0.70;
    public const double ManagerPersonalWeight = 0.30;
    public const int ManagerReviewPenaltyPerReport = 3;
    public const int MaximumManagerReviewPenalty = 15;

    public static int CalculatePersonalScore(int bonusPoints, int penaltyPoints)
        => Math.Clamp(BaseScore + bonusPoints - penaltyPoints, MinimumScore, MaximumPersonalScore);

    public static int CalculateManagerScore(
        double unitAverageScore,
        int personalScore,
        int reviewPenaltyPoints)
        => Math.Max(
            MinimumScore,
            (int)Math.Round(
                unitAverageScore * ManagerUnitWeight + personalScore * ManagerPersonalWeight) -
            reviewPenaltyPoints);

    public static decimal CalculateRate(int numerator, int denominator)
    {
        if (denominator <= 0)
            return 0m;

        return Math.Round((decimal)numerator / denominator * 100m, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal? CalculateEstimationAccuracy(decimal plannedHours, decimal actualHours)
    {
        if (plannedHours <= 0m)
            return null;

        var varianceRate = Math.Abs(actualHours - plannedHours) / plannedHours * 100m;
        return Math.Round(Math.Max(0m, 100m - varianceRate), 2, MidpointRounding.AwayFromZero);
    }
}
