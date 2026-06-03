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
    }
}
