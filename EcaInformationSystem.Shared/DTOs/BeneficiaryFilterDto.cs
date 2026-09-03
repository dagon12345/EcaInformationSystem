namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryFilterDto
    {
        public List<Guid> Ids { get; set; } = new();
        public int? FilterQuarter { get; set; }
        public string? FilterBatch { get; set; }
        public int? FilterRefYear { get; set; }
        public string? FilterRegionRoman { get; set; } // e.g. "XIII", "X"

        public int? PsgcCodeRegion { get; set; }
        public int? PsgcCodeProvince { get; set; }
        public int? PsgcCodeMunicipality { get; set; }
        public int? PsgcCodeBarangay { get; set; }

        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? Suffix { get; set; }
        public string? FullName { get; set; }
        public int? SpecificAge { get; set; }
        public string? Validator { get; set; }
        public string? BatchCode { get; set; }
        public DateTime? SpecificBirthday { get; set; }
        public DateTime? BirthdayFrom { get; set; }
        public DateTime? BirthdayTo { get; set; }
        public bool? OnlyEightyYearsOld { get; set; }
        public int? MilestoneYear { get; set; }
        public int? Sex { get; set; }
        // For filtering by PayrollQuarter
        public int? FilterPayrollQuarter { get; set; }
        public int? FilterFiscalYear { get; set; }
        public int? PaymentStatus { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        // For multi-select payroll quarters
        public List<int>? FilterPayrollQuarters { get; set; }
        public List<int> PsgcCodeProvinces { get; set; } = new();
        public List<int> PsgcCodeMunicipalities { get; set; } = new();
        public List<int> PsgcCodeBarangays { get; set; } = new();

        public List<int> PaymentStatuses { get; set; } = new();

        public DateTime? PaymentDateFrom { get; set; }
        public DateTime? PaymentDateTo { get; set; }
        // Add these alongside your existing PaymentDateFrom/PaymentDateTo
        public DateTime? DateAddedFrom { get; set; }
        public DateTime? DateAddedTo { get; set; }
        public DateTime? DateEndorsedFrom { get; set; }
        public DateTime? DateEndorsedTo { get; set; }

        public int? FindingStatus { get; set; }

        public string? SortColumn { get; set; } //Birthdate and MilestoneYear
        public bool SortAscending { get; set; } = true;

        public bool? IsCompliant { get; set; }
        public bool? IsEligible { get; set; }

        public string? ComplianceMode { get; set; }
        public string? EligibilityMode { get; set; }
        public string? GeneralSearch { get; set; } // General search - scans all relevant columns with OR logic
        public int? CoStatus { get; set; }
        // 0 = Not Replaced, 1 = Replaced, 2 = Is Replacement
        public int? ReplacementStatus { get; set; }
        public int? FilterModeOfPayment { get; set; }
        public string? DataQualityIssue { get; set; } // "location" | "headsup" | "incomplete"

        // Nullable — null means "no filter", matching IsCompliant/IsEligible above
        public bool? IsLivenessVerified { get; set; }
        public bool? IsReadyForEft { get; set; }

        // Default false — records that are the newer/duplicate side of a
        // Known Duplicate pair (see BeneficiaryDuplicateHistory) are excluded
        // from results and counts unless this is explicitly set true. Keeps
        // them out of the grid and every statistic by default, on request.
        public bool IncludeKnownDuplicates { get; set; }

    }
}
