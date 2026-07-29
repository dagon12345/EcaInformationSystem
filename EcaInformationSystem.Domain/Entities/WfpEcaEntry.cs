using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    // One row per (RegionCode, FiscalYear, UacsCode) — the Work Financial Plan
    // line item for that region/year. Admins build this list from scratch, one
    // row at a time; there's no fixed UACS catalog behind it.
    public class WfpEcaEntry
    {
        public Guid Id { get; set; }

        public int RegionCode { get; set; }
        public int FiscalYear { get; set; }
        public string UacsCode { get; set; } = string.Empty;
        public string UacsName { get; set; } = string.Empty;

        // Preserves table row order, including where an admin-added row lands
        // relative to the standard catalog — reassigned to the submitted array
        // position on every save, so reordering the grid client-side just works.
        public int SortOrder { get; set; }

        public decimal Allotment { get; set; }
        public decimal Obligation { get; set; }
        public string? Remarks { get; set; }

        public DateTime DateSet { get; set; }
        public string SetBy { get; set; } = string.Empty;
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = default!;

        public decimal Balance => Allotment - Obligation;
    }
}
