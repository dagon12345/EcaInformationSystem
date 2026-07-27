using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    // One row per (RegionCode, FiscalYear) — the PDO admin/super admin for that
    // region sets an annual grantee-served target here, split across 12 months
    // however the region has organized its rollout (not necessarily even).
    public class AnnualGranteeTarget
    {
        public Guid Id { get; set; }

        public int RegionCode { get; set; }
        public int FiscalYear { get; set; }

        public int JanTarget { get; set; }
        public int FebTarget { get; set; }
        public int MarTarget { get; set; }
        public int AprTarget { get; set; }
        public int MayTarget { get; set; }
        public int JunTarget { get; set; }
        public int JulTarget { get; set; }
        public int AugTarget { get; set; }
        public int SepTarget { get; set; }
        public int OctTarget { get; set; }
        public int NovTarget { get; set; }
        public int DecTarget { get; set; }

        public DateTime DateSet { get; set; }
        public string SetBy { get; set; } = string.Empty;
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = default!;

        public int AnnualTotal =>
            JanTarget + FebTarget + MarTarget + AprTarget + MayTarget + JunTarget +
            JulTarget + AugTarget + SepTarget + OctTarget + NovTarget + DecTarget;

        public int[] ToMonthlyArray() => new[]
        {
            JanTarget, FebTarget, MarTarget, AprTarget, MayTarget, JunTarget,
            JulTarget, AugTarget, SepTarget, OctTarget, NovTarget, DecTarget
        };

        public void SetMonthlyTargets(int[] monthly)
        {
            JanTarget = monthly[0];
            FebTarget = monthly[1];
            MarTarget = monthly[2];
            AprTarget = monthly[3];
            MayTarget = monthly[4];
            JunTarget = monthly[5];
            JulTarget = monthly[6];
            AugTarget = monthly[7];
            SepTarget = monthly[8];
            OctTarget = monthly[9];
            NovTarget = monthly[10];
            DecTarget = monthly[11];
        }
    }
}
