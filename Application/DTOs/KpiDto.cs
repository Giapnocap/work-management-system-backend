using System.ComponentModel.DataAnnotations;

namespace WorkManagementSystem.Application.DTOs
{
    public class KpiPeriodDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Monthly";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Open";
        public DateTime CreatedAt { get; set; }
        public DateTime? LockedAt { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class CreateKpiPeriodDto
    {
        [MaxLength(100, ErrorMessage = "Ten ky KPI toi da 100 ky tu.")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(30, ErrorMessage = "Loai ky KPI toi da 30 ky tu.")]
        public string Type { get; set; } = "Monthly";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
