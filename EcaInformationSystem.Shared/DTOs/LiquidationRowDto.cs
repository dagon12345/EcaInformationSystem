namespace EcaInformationSystem.Shared.DTOs
{
    /// <summary>
    /// Represents one grouped CDR data row:
    /// Municipality + Province + PaymentDate + MilestoneYear → one row
    /// </summary>
    public class LiquidationRowDto
    {
        public DateTime PaymentDate { get; set; }
        public string CgpNumber { get; set; } = string.Empty;
        public string Payee { get; set; } = string.Empty;
        public string NatureOfPayment { get; set; } = string.Empty;
        public decimal Disbursement { get; set; }
        public decimal CashAdvanceBalance { get; set; }
        public int MilestoneYear { get; set; }
        public string MunicipalityName { get; set; } = string.Empty;
        public string ProvinceName { get; set; } = string.Empty;
    }

    /// <summary>Preview row shown in modal before export</summary>
    public class LiquidationPreviewRowDto
    {
        public DateTime PaymentDate { get; set; }
        public string CgpNumber { get; set; } = string.Empty;
        public string Payee { get; set; } = string.Empty;
        public string NatureOfPayment { get; set; } = string.Empty;
        public decimal Disbursement { get; set; }
        public decimal RunningBalance { get; set; }
        public string MunicipalityName { get; set; } = string.Empty;
        public string ProvinceName { get; set; } = string.Empty;
        public int MilestoneYear { get; set; }
        public int Count { get; set; }
    }
}