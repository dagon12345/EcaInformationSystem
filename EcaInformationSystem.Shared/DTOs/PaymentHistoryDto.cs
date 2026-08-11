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

        // ── Replacement Status — moved here from the beneficiary level ───────
        public int? ReplacementStatus { get; set; } // null/0 Not Replaced, 1 Replaced, 2 Is Replacement
        public DateTime? ReplacementDate { get; set; }
        public string? ReplacementRemarks { get; set; }

        // Display-only — resolved server-side so the UI doesn't need a second
        // round trip to show who/what this entry is linked to.
        public string? LinkedBeneficiaryName { get; set; }
        public string? LinkedPeriodLabel { get; set; }       // e.g. "Q1 2026"
        public string? LinkedPaymentStatusLabel { get; set; } // e.g. "Paid"
    }

    public class ReplacePaymentHistoryRequestDto
    {
        public Guid OutgoingPaymentHistoryId { get; set; }
        public Guid IncomingPaymentHistoryId { get; set; }
        public DateTime? ReplacementDate { get; set; }
        public string? Remarks { get; set; }
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