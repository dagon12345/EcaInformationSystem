namespace EcaInformationSystem.Shared.DTOs
{
    public class AnnualGranteeTargetDto
    {
        public Guid Id { get; set; }
        public int RegionCode { get; set; }
        public string? RegionName { get; set; } 
        public int FiscalYear { get; set; }

        // Index 0 = January ... index 11 = December.
        public int[] MonthlyTargets { get; set; } = new int[12];
        public int AnnualTotal { get; set; }

        public DateTime DateSet { get; set; }
        public string? SetBy { get; set; }
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class UpsertAnnualGranteeTargetDto
    {
        public int FiscalYear { get; set; }

        // Index 0 = January ... index 11 = December. Doesn't have to be evenly
        // split — the PDO organizes its own monthly rollout.
        public int[] MonthlyTargets { get; set; } = new int[12];
    }

    public class MonthlyTargetVsActualDto
    {
        public int Month { get; set; } // 1-12
        public string MonthName { get; set; } = string.Empty;
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
        public List<MonthlyTargetVsActualDto> Monthly { get; set; } = new();

        // Who set/last touched this target, for the "last set by" line in the UI.
        public DateTime? DateSet { get; set; }
        public string? SetBy { get; set; }
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }
    }
}
