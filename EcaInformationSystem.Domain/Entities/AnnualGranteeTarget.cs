using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    // One row per (RegionCode, FiscalYear) — the PDO admin/super admin for that
    // region sets an annual grantee-served target here, split across the 4
    // payroll quarters however the region has organized its rollout (not
    // necessarily even).
    public class AnnualGranteeTarget
    {
        public Guid Id { get; set; }

        public int RegionCode { get; set; }
        public int FiscalYear { get; set; }

        public int Q1Target { get; set; }
        public int Q2Target { get; set; }
        public int Q3Target { get; set; }
        public int Q4Target { get; set; }

        public DateTime DateSet { get; set; }
        public string SetBy { get; set; } = string.Empty;
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = default!;

        public int AnnualTotal => Q1Target + Q2Target + Q3Target + Q4Target;

        public int[] ToQuarterlyArray() => new[] { Q1Target, Q2Target, Q3Target, Q4Target };

        public void SetQuarterlyTargets(int[] quarterly)
        {
            Q1Target = quarterly[0];
            Q2Target = quarterly[1];
            Q3Target = quarterly[2];
            Q4Target = quarterly[3];
        }
    }
}
