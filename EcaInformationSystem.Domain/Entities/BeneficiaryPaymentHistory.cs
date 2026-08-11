using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    public class BeneficiaryPaymentHistory
    {
        public Guid Id { get; set; }

        // ── Foreign key to the beneficiary this payment belongs to ──────────
        public Guid BeneficiaryInformationId { get; set; }
        public BeneficiaryInformation? Beneficiary { get; set; }

        // ── Payroll period this payment event belongs to ─────────────────────
        // Nullable because a record can exist before a quarter/year is assigned
        // (e.g. "Pending" status entered before payroll batching happens).
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; }

        // ── Payment facts ─────────────────────────────────────────────────────
        public int PaymentStatus { get; set; }   // 0 N/A, 1 Unpaid, 2 Paid, 3 Pending
        public int ModeOfPayment { get; set; }   // 0 None, 1 Cash Advance by SDO, 2 Bank Transfer
        public DateTime? PaymentDate { get; set; }
        public string? Remarks { get; set; }

        // ── Audit trail — who/when created this specific history entry ───────
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;

        // ── Audit trail for corrections — distinct from DateCreated ──────────
        // DateCreated never changes after insert; these track if/when someone
        // later edited THIS SAME entry (a correction), as opposed to a new
        // entry being added (a new payment event). Keeping this separate lets
        // you tell "this was corrected" apart from "this was the original."
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }

        // ── Replacement Status — moved here from BeneficiaryInformation. A
        // replacement is about ONE specific payment history entry (e.g. "their
        // Q1 2026 payout was handed to grantee B's Q1 2026 entry"), not the whole
        // beneficiary. null/0 = Not Replaced (default). 1 = Replaced — this
        // entry's payout was handed over, ReplacedByPaymentHistoryId points to
        // the incoming entry. 2 = Is Replacement — this entry took over another
        // entry's payout, ReplacesPaymentHistoryId points back to the outgoing
        // entry. Both sides are updated together so the link stays symmetric.
        // Plain Guid?, no FK/nav mapping — same pattern the old beneficiary-level
        // fields used, avoids multi-cascade-path issues on a self-referencing table.
        public int? ReplacementStatus { get; set; }
        public Guid? ReplacedByPaymentHistoryId { get; set; }
        public Guid? ReplacesPaymentHistoryId { get; set; }
        public DateTime? ReplacementDate { get; set; }
        public string? ReplacementRemarks { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = default!;
    }
}