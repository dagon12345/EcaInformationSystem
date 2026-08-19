namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryImportResultDto
    {
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int ImportedCount { get; set; }
        public int ErrorCount { get; set; }
        public int SkippedDuplicateCount { get; set; }
        public bool HasErrors => ErrorCount > 0;
        public bool IsSuccess => ErrorCount == 0;
        public List<Guid> ImportedIds { get; set; } = new();
        public List<BeneficiaryImportErrorDto> Errors { get; set; } = new();
    }

    public class BeneficiaryImportErrorDto
    {
        public int RowNumber { get; set; }
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RawValue { get; set; }
        public string? Suggestion { get; set; }

        // ✅ NEW — set only when there's a concrete, machine-usable correction
        // (e.g. a spelling match found in the PSGC lookup). SuggestedValue is
        // the clean value to apply ("AGUSAN DEL NORTE"); CorrectionField is a
        // stable key ("Region"/"Province"/"Municipality"/"Barangay") the
        // client echoes back — decoupled from the human-readable Field label,
        // which varies in casing per error type. Errors without a concrete
        // suggestion (e.g. "First Name is required", or a spelling miss with
        // no PSGC match) leave both null — those still require editing the
        // file, there's nothing to auto-accept.
        public string? SuggestedValue { get; set; }
        public string? CorrectionField { get; set; }

        // ✅ NEW — the row's current Region/Province/Municipality text (as
        // typed in the file, whether or not it resolved), shown as reference
        // columns next to a Region/Province/Municipality/Barangay error so
        // the user has context for the Location Picker's cascade instead of
        // guessing blind — e.g. a Barangay error still shows which
        // Municipality the row was meant to be in.
        public string? RowRegion { get; set; }
        public string? RowProvince { get; set; }
        public string? RowMunicipality { get; set; }
    }
}
