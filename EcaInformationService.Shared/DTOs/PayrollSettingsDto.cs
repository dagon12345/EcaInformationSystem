// ============================================================
// NEW FILE: EcaInformationSystem.Shared/DTOs/PayrollSettingsDto.cs
// ============================================================
namespace EcaInformationSystem.Shared.DTOs
{
    /// <summary>
    /// All configurable fields for Cash Gift Payroll generation.
    /// Sent from the Blazor modal → API → Service.
    /// </summary>
    public class PayrollSettingsDto
    {
        // ── Selected record IDs ──────────────────────────────────────────────
        public List<Guid> Ids { get; set; } = new();

        // ── CGP Number segments ──────────────────────────────────────────────
        /// <summary>e.g. "RegionXIII" — defaults to "RegionXIII"</summary>
        public string RegionCode { get; set; } = "RegionXIII";

        /// <summary> Month, e.g. "03" — defaults to current month</summary>
        public string Month { get; set; } = DateTime.Today.ToString("MM");

        /// <summary>Fixed segment "01" per current format (configurable for future)</summary>
        public string FixedSegment { get; set; } = "01";

        /// <summary>Batch run number, e.g. "26" — user configurable</summary>
        public string ShortenYear { get; set; } = DateTime.Today.ToString("yy");

        // ── Signatory 1: Prepared/Certified by (Focal Person) ────────────────
        public string Signatory1Name { get; set; } = "SARAH ROSE M. SALINGAY";
        public string Signatory1Position { get; set; } = "PDO IV / ECA Focal Person";
        public string Signatory1Label { get; set; } = "I hereby certify that each person whose name appears on the payroll are entitled to cash gift.";

        // ── Signatory 2: Approved by (Regional Director) ─────────────────────
        public string Signatory2Name { get; set; } = "CESAR A. ADEGUE IV, Ph. D, CESE";
        public string Signatory2Position { get; set; } = "Concurrent Regional Director";
        public string Signatory2Label { get; set; } = "Approved for Payment:";

        // ── Signatory 3: SDO (bottom-left) ────────────────────────────────────
        public string Signatory3Name { get; set; } = "ALMIRA C. REBUCAS";
        public string Signatory3Position { get; set; } = "Printed Name and Signature of SDO";

        // ── Signatory 4: Other officer (bottom-right) ─────────────────────────
        public string Signatory4Name { get; set; } = "";
        public string Signatory4Position { get; set; } = "Other officer present during Payout";

        // ── Records per page ──────────────────────────────────────────────────
        /// <summary>How many beneficiary rows fit on one legal-landscape page (default 15)</summary>
        public int RecordsPerPage { get; set; } = 15;

        // ── Cash gift amount ──────────────────────────────────────────────────
        public decimal CashGiftAmount { get; set; } = 10_000m;
    }
}