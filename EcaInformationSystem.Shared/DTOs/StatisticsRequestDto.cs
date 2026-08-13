namespace EcaInformationSystem.Shared.DTOs
{
    public class StatisticsRequestDto
    {
        public int? Region { get; set; }
        public int? Province { get; set; }
        public int? Municipality { get; set; }
        public int MilestoneYear { get; set; }  // 2024, 2025, 2026
        public int MilestoneAge { get; set; }   // 80, 85, 90, 95, 100
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
        public int Sex { get; set; }
        public int Age { get; set; }
        public string? ProvinceName { get; set; }
        public string? MunicipalityName { get; set; }
        public int PaymentStatus { get; set; }
        public List<PaymentHistoryDto> PaymentHistories { get; set; } = new();
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
