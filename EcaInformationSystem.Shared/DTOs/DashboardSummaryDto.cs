namespace EcaInformationSystem.Shared.DTOs
{
    public class DashboardSummaryDto
    {
        public int TotalBeneficiaries { get; set; }
        public int PaidCount { get; set; }
        public int UnpaidCount { get; set; }
        public int PendingCount { get; set; }
        public int NotApplicableCount { get; set; }
        public decimal TotalDisbursement { get; set; }
    }
}
