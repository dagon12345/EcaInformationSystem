namespace EcaInformationSystem.Shared.DTOs
{
    public class LiquidationSettingsDto
    {
        // Header settings
        public string RegionOrgUnit { get; set; } = "NCSC Cluster 8 - Caraga Region";
        public string NewOrgCode { get; set; } = "26 045  0000000";
        public string FundCluster { get; set; } = "101 Regular Agency Fund";

        // Accountable Officer block
        public string AccountableOfficerName { get; set; } = string.Empty;
        public string OfficialDesignation { get; set; } = string.Empty;
        public string Station { get; set; } = "NCSC - CARAGA REGION";

        // Modified Row (first data row - cash advance receipt)
        public DateTime InputDate { get; set; } = DateTime.Today;
        public string DvYear { get; set; } = DateTime.Today.Year.ToString();
        public string DvMonth { get; set; } = DateTime.Today.Month.ToString("D2");
        // DV Reference auto-generates as: DV: {DvYear}-{DvMonth}-0001
        public string DvPayee { get; set; } = string.Empty;
        public string NatureOfPayment { get; set; } = string.Empty;
        public decimal InitialCashAdvance { get; set; } = 14_000_000m;

        // CGP settings
        // CGP format: CGP No.: {RegionCode}-{MilestoneYear}{Month}-{FixedSegment}-{ShortenYear}-{counter:D4}
        // Example:    CGP No.: RegionXIII-202403-01-26-0002
        public string RegionCode { get; set; } = "RegionXIII";
        public string FixedSegment { get; set; } = "01";
        public string ShortenYear { get; set; } = "26";

        // Certification block (final sheet)
        public string CertificationPeriodFrom { get; set; } = string.Empty; // e.g. March 17, 2026
        public string CertificationPeriodTo { get; set; } = string.Empty;   // e.g. April 17, 2026
        public DateTime CertificationDate { get; set; } = DateTime.Today;

        // Rows per page (A4 portrait fits ~8 data rows comfortably)
        public int RowsPerPage { get; set; } = 8;
    }
}