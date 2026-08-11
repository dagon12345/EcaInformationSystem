using Microsoft.AspNetCore.Http;

namespace EcaInformationSystem.Shared.DTOs
{
    public class SeniorCitizenDirectoryImportErrorDto
    {
        public int RowNumber { get; set; }
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RawValue { get; set; }
    }

    // Unlike Beneficiary's fuzzy name/birthdate matching, this directory is one
    // entry per municipality — a "duplicate" is an exact match on an already-
    // active municipality, so MatchScore is always 1.0. Kept as a field (rather
    // than a bare bool) so the client can reuse the same score-badge UI pattern
    // as the Beneficiary import's soft-duplicate review modal.
    public class SeniorCitizenDirectorySoftDuplicateDto
    {
        public int RowNumber { get; set; }
        public string ProvinceName { get; set; } = string.Empty;
        public string MunicipalityName { get; set; } = string.Empty;
        public string? ExistingLswdoName { get; set; }
        public string? ExistingOscaHeadName { get; set; }
        public string? ExistingMayorName { get; set; }
        public DateTime? ExistingUpdatedAt { get; set; }
        public double MatchScore { get; set; }
    }

    public class SeniorCitizenDirectoryPreviewResultDto
    {
        public List<SeniorCitizenDirectoryImportErrorDto> HardErrors { get; set; } = new();
        public List<SeniorCitizenDirectorySoftDuplicateDto> SoftDuplicates { get; set; } = new();
        public int TotalRows { get; set; }
        public int CleanRows { get; set; }
    }

    public class SeniorCitizenDirectoryImportResultDto
    {
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int ImportedCount { get; set; }
        public int ErrorCount { get; set; }
        public int SkippedDuplicateCount { get; set; }
        public bool HasErrors { get; set; }
        public bool IsSuccess { get; set; }
        public List<Guid> ImportedIds { get; set; } = new();
        public List<SeniorCitizenDirectoryImportErrorDto> Errors { get; set; } = new();
    }

    public class ImportSeniorCitizenDirectoryExcelSheetRequestDto
    {
        public IFormFile File { get; set; } = default!;
    }
}
