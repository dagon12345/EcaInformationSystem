using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    // Centralized, shared CARAGA Senior Citizens Directory — one row per
    // municipality/city, editable by PDO/Admin/SuperAdmin, view-only for
    // everyone else. Mirrors the CARAGA Senior Citizens Directory survey form.
    public class SeniorCitizenDirectoryEntry
    {
        public Guid Id { get; set; }

        // ── Location — one entry per municipality/city ──────────────────────
        public int PsgcCodeRegion { get; set; }
        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }

        public string? IncomeClassification { get; set; }
        public int? SeniorCitizensPopulation { get; set; }
        public string? PopulationAsOfNote { get; set; }

        // ── Local Social Welfare and Development Officer (LSWDO) ────────────
        public string? LswdoName { get; set; }
        public string? LswdoPosition { get; set; }
        public string? LswdoContactNumber { get; set; }
        public string? LswdoEmail { get; set; }

        // ── SC Focal ──────────────────────────────────────────────────────
        public string? ScFocalName { get; set; }
        public string? ScFocalContactNumber { get; set; }
        public string? ScFocalEmail { get; set; }

        // ── OSCA Head ─────────────────────────────────────────────────────
        public string? OscaHeadName { get; set; }
        public string? OscaHeadLengthOfService { get; set; }
        public string? OscaHeadContactNumber { get; set; }
        public string? OscaHeadEmail { get; set; }

        // ── FSCAP President ───────────────────────────────────────────────
        public string? FscapPresidentName { get; set; }
        public string? FscapPresidentContactNumber { get; set; }
        public string? FscapPresidentEmail { get; set; }
        public string? FscapPresidentLengthOfService { get; set; }

        // ── Senior Citizen Center (SCC) ───────────────────────────────────
        public bool? HasSeniorCitizenCenter { get; set; }
        public bool? IsSccAccredited { get; set; }
        public string? SccAccreditationValidity { get; set; }
        public string? WithoutSccResourcesNote { get; set; }
        public string? SccManagedBy { get; set; }
        public string? ServicesOffered { get; set; }

        // ── LGU Cash Incentive ────────────────────────────────────────────
        public bool? HasCashIncentive { get; set; }
        public string? CashIncentiveDetails { get; set; }
        public bool? HasSupportingOrdinance { get; set; }
        public string? OrdinanceDocumentLinks { get; set; }

        // ── VAOP (Violence Against Older Persons) Help Desk ──────────────
        public bool? HasVaopHelpDesk { get; set; }
        public string? VaopReferralMechanism { get; set; }

        // ── City / Municipal Mayor ────────────────────────────────────────
        public string? MayorName { get; set; }
        public string? MayorOfficeEmail { get; set; }

        // ── Bookkeeping ───────────────────────────────────────────────────
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = default!;
    }
}
