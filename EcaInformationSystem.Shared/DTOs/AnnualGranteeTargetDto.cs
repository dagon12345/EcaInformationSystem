namespace EcaInformationSystem.Shared.DTOs
{
    public class AnnualGranteeTargetDto
    {
        public Guid Id { get; set; }
        public int RegionCode { get; set; }
        public string? RegionName { get; set; }
        public int FiscalYear { get; set; }

        // Index 0 = Q1 ... index 3 = Q4.
        public int[] QuarterlyTargets { get; set; } = new int[4];
        public int AnnualTotal { get; set; }

        public DateTime DateSet { get; set; }
        public string? SetBy { get; set; }
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class UpsertAnnualGranteeTargetDto
    {
        public int FiscalYear { get; set; }

        // Index 0 = Q1 ... index 3 = Q4. Doesn't have to be evenly split — the
        // PDO organizes its own quarterly rollout.
        public int[] QuarterlyTargets { get; set; } = new int[4];
    }

    public class QuarterTargetVsActualDto
    {
        public int Quarter { get; set; } // 1-4
        public string QuarterLabel { get; set; } = string.Empty; // "Q1".."Q4"
        public int Target { get; set; }
        public int PaidCount { get; set; }
    }

    public class AnnualTargetComparisonDto
    {
        public int RegionCode { get; set; }
        public string? RegionName { get; set; }
        public int FiscalYear { get; set; }

        // False when no admin has set a target yet for this region/year —
        // the UI shows an empty/"not set" state instead of a 0-target chart.
        public bool HasTarget { get; set; }

        public int AnnualTarget { get; set; }
        public int TotalPaidYtd { get; set; }
        public List<QuarterTargetVsActualDto> Quarterly { get; set; } = new();

        // Who set/last touched this target, for the "last set by" line in the UI.
        public DateTime? DateSet { get; set; }
        public string? SetBy { get; set; }
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }
    }
}
