namespace EcaInformationSystem.Shared.DTOs
{
    public class StatisticsRequestDto
    {
        public int? Region { get; set; }
        public int? Province { get; set; }
        public int? Municipality { get; set; }
        public int MilestoneYear { get; set; }  // 2024, 2025, 2026
        public int MilestoneAge { get; set; }   // 80, 85, 90, 95, 100
        public int PaymentStatus { get; set; } = -1;
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; } // ✅ new
    }
}
