namespace EcaInformationSystem.Shared.DTOs
{
    // One row of the "Download as Excel" export from the Statistics page's
    // Total Grantees card — every grantee currently matching the page's
    // active filters, with the full Annex A Section E (bank/payout account)
    // detail so the reader can tell which channel/bank is actually active
    // for each grantee, not just a beneficiary summary.
    public class StatisticsGranteeExportRowDto
    {
        public string? BatchCode { get; set; }
        public string? OscaIdNumber { get; set; }
        public int? NcscRrn { get; set; }
        public string? LastName { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        public DateTime BirthDate { get; set; }
        public int Age { get; set; }
        public string Sex { get; set; } = string.Empty;

        public string? RegionName { get; set; }
        public string? ProvinceName { get; set; }
        public string? MunicipalityName { get; set; }
        public string? BarangayName { get; set; }
        public string ContactNumber { get; set; } = string.Empty;

        public bool IsCompliant { get; set; }
        public string? Validator { get; set; }
        public DateTime ValidationDate { get; set; }
        public string? Remarks { get; set; }

        public string PaymentStatusLabel { get; set; } = string.Empty;
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; }
        public string ModeOfPaymentLabel { get; set; } = string.Empty;
        public string CoStatusLabel { get; set; } = string.Empty;
        public int MilestoneYear { get; set; }
        public bool? IsLivenessVerified { get; set; }
        public bool? IsReadyForEft { get; set; }

        // ── Annex A Section E — payout account, "what bank is active" ──────
        public string PreferredChannelLabel { get; set; } = string.Empty;
        public string? BankOrWalletName { get; set; }
        public string? AccountNumber { get; set; }
        public string? BranchName { get; set; }
        public string? BankAddress { get; set; }
        public string? GCashName { get; set; }
        public string? GCashOrMobileNumber { get; set; }
        public bool? IsJointAccount { get; set; }
        public string? SwiftCode { get; set; }
        public string? Iban { get; set; }
    }
}
