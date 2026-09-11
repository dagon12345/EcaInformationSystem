namespace EcaInformationSystem.Shared.DTOs
{
    public class StatisticsRequestDto
    {
        public int? Region { get; set; }
        public int? Province { get; set; }
        public int? Municipality { get; set; }
        public int MilestoneYear { get; set; }  // 2024, 2025, 2026
        public int MilestoneAge { get; set; }   // 80, 85, 90, 95, 100
        // ✅ NEW — separate, additive "anticipation" filter: multi-select
        // 2024/2025/2026, independent of MilestoneYear above (which stays a
        // single-year filter with its original behavior, untouched). A
        // beneficiary matches if they turn ANY milestone age (80/85/90/95/100)
        // in ANY of the selected years — including years whose milestone
        // birthday hasn't happened yet, so PDOs can anticipate who's coming
        // up across all three years at once instead of checking one at a time.
        public List<int>? AnticipatedMilestoneYears { get; set; }
        // ✅ CHANGED — multi-select: null/empty = no filter (was a single int
        // with -1 meaning "all"). A beneficiary matches if ANY selected status
        // is found on their (period-scoped) payment history record.
        public List<int>? PaymentStatuses { get; set; }
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; } // ✅ new
        public DateTime? DateEndorsedFrom { get; set; }
        public DateTime? DateEndorsedTo { get; set; }
        public DateTime? DateAddedFrom { get; set; }
        public DateTime? DateAddedTo { get; set; }
        // ✅ new — nullable, same "null = no filter" convention as everything above
        public bool? IsLivenessVerified { get; set; }
        public bool? IsReadyForEft { get; set; }

        // ✅ new — CO (Central Office) endorsement/approval dates, distinct
        // from DateEndorsed above (that's the application's own endorsement
        // date; these are CoDateEndorsed/CoDateApproved on the same entity).
        public DateTime? CoDateEndorsedFrom { get; set; }
        public DateTime? CoDateEndorsedTo { get; set; }
        public DateTime? CoDateApprovedFrom { get; set; }
        public DateTime? CoDateApprovedTo { get; set; }
    }

    // ✅ NEW — powers the Statistics page's "audit this count" modal: same
    // filters as StatisticsRequestDto, plus which summary-card bucket to
    // list ("all", "paid", "unpaid", "pending", "notapplicable", "male", "female").
    public class StatisticsMembersRequestDto
    {
        public StatisticsRequestDto Filter { get; set; } = new();
        public string Bucket { get; set; } = "all";
        // ✅ NEW — pagination, so a bucket with thousands of grantees doesn't
        // pull every one of their payment histories into one response.
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    // One grantee included in an audited count, with their full payment
    // history so an auditor can see every payment event behind their
    // current status, not just the latest one.
    public class StatisticsMemberDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? OscaIdNumber { get; set; }
        public string? BatchCode { get; set; }
        public int Sex { get; set; }
        public int MilestoneYear { get; set; }
        public int Age { get; set; }
        public string? ProvinceName { get; set; }
        public string? MunicipalityName { get; set; }
        public int PaymentStatus { get; set; }
        public List<PaymentHistoryDto> PaymentHistories { get; set; } = new();

        // ✅ NEW — Annex A Section E (payout account) detail, so the audit
        // modal shows which bank/channel is actually active per grantee.
        public string PreferredChannelLabel { get; set; } = string.Empty;
        public string? BankOrWalletName { get; set; }
        public string? AccountNumber { get; set; }
        public string? GCashOrMobileNumber { get; set; }
        public string? BranchName { get; set; }

        // ✅ NEW — Liveness/EFT status + date, own columns in the audit modal
        // (was only visible via a separate summary card before).
        public bool? IsLivenessVerified { get; set; }
        public DateTime? DateOfLiveness { get; set; }
        public bool? IsReadyForEft { get; set; }
    }

    // ✅ NEW — paged wrapper for the audit modal's grantee list.
    public class StatisticsMembersPagedResultDto
    {
        public List<StatisticsMemberDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
