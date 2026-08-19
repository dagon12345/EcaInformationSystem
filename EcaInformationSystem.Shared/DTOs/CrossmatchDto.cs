namespace EcaInformationSystem.Shared.DTOs
{
    public enum CrossmatchStatus
    {
        New = 0,
        Possible = 1,
        Existing = 2,
        Error = 3
    }

    public class CrossmatchRowDto
    {
        public int RowNumber { get; set; }
        public CrossmatchStatus Status { get; set; }
        public double MatchScore { get; set; }
        public List<string> ParseErrors { get; set; } = new();

        // ── Existing-record match info (Status = Possible / Existing) ──────
        public Guid? ExistingId { get; set; }
        public string? ExistingFullName { get; set; }
        public DateTime? ExistingBirthDate { get; set; }
        public string? ExistingOscaId { get; set; }
        public string? ExistingMunicipality { get; set; }
        public string? ExistingBarangay { get; set; }
        public string? MatchReason { get; set; }

        // ── Parsed row data — reused to prefill a create request ───────────
        public string? BatchCode { get; set; }
        public DateTime? DateApplied { get; set; }
        public DateTime? DateEndorsed { get; set; }
        public string? OscaIdNumber { get; set; }
        public DateTime? OscaIdDateIssued { get; set; }
        public int? NcscRrn { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        public DateTime? BirthDate { get; set; }
        public int Sex { get; set; }
        public int? Citizenship { get; set; }
        public int PsgcCodeRegion { get; set; }
        public string? RegionName { get; set; }
        public int PsgcCodeProvince { get; set; }
        public string? ProvinceName { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public string? MunicipalityName { get; set; }
        public int PsgcCodeBarangay { get; set; }
        public string? BarangayName { get; set; }
        public string? ContactNumber { get; set; }
        public bool IsDeceased { get; set; }
        public DateTime? DateOfDeath { get; set; }
        public bool? IsIndigenousPeople { get; set; }
        public bool? IsPersonWithDisability { get; set; }
    }

    public class CrossmatchResultDto
    {
        public int TotalRows { get; set; }
        public int NewCount { get; set; }
        public int PossibleCount { get; set; }
        public int ExistingCount { get; set; }
        public int ErrorCount { get; set; }
        public List<CrossmatchRowDto> Rows { get; set; } = new();
    }

    // ✅ NEW — polled by the client while a background crossmatch job runs.
    public class CrossmatchJobStatusDto
    {
        public string Status { get; set; } = "Running";
        public int Processed { get; set; }
        public int Total { get; set; }
        public CrossmatchResultDto? Result { get; set; }
        public string? Error { get; set; }
    }

    public class CrossmatchJobStartResultDto
    {
        public Guid JobId { get; set; }
    }
}
