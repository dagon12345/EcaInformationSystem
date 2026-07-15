namespace EcaInformationSystem.Shared.DTOs
{
    public class PaymentHistoryDto
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; }
        public int PaymentStatus { get; set; }
        public int ModeOfPayment { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? Remarks { get; set; }
        public DateTime DateCreated { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }
        public bool IsCurrent { get; set; }
    }

    public class AddPaymentHistoryDto
    {
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; }
        public int PaymentStatus { get; set; }
        public int? ModeOfPayment { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? Remarks { get; set; }
    }

    public class BulkAddPaymentHistoryRequestDto
    {
        public List<Guid> Ids { get; set; } = new();
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; }
        public int PaymentStatus { get; set; }
        public int? ModeOfPayment { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? Remarks { get; set; }
    }

    public class EditPaymentHistoryRequestDto
    {
        public Guid HistoryId { get; set; }
        public Guid BeneficiaryId { get; set; }
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; }
        public int PaymentStatus { get; set; }
        public int? ModeOfPayment { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? Remarks { get; set; }
    }
}