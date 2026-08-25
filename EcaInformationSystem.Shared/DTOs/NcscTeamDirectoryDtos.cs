using Microsoft.AspNetCore.Http;

namespace EcaInformationSystem.Shared.DTOs
{
    public class NcscTeamDirectoryEntryDto
    {
        public Guid Id { get; set; }
        public int PsgcCodeRegion { get; set; }
        public string? RegionName { get; set; }
        public string? Position { get; set; }
        public string? FullName { get; set; }
        public string? Nickname { get; set; }
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class UpsertNcscTeamDirectoryEntryDto
    {
        public Guid? Id { get; set; }
        public int PsgcCodeRegion { get; set; }
        public string? Position { get; set; }
        public string? FullName { get; set; }
        public string? Nickname { get; set; }
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class NcscTeamDirectoryHistoryDto
    {
        public Guid Id { get; set; }
        public string Activity { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    // Live add/edit/delete broadcast payload — mirrors SeniorCitizenDirectoryChangeDto,
    // but this directory is nationwide (not region-scoped), so the hub broadcasts
    // to every connected client rather than a per-region group.
    public class NcscTeamDirectoryChangeDto
    {
        public Guid Id { get; set; }
        public string ChangeType { get; set; } = string.Empty; // "Created" | "Updated" | "Deleted"
        public NcscTeamDirectoryEntryDto? Entry { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
    }

    public class NcscTeamDirectoryImportErrorDto
    {
        public int RowNumber { get; set; }
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RawValue { get; set; }
    }

    // No soft-duplicate concept here — unlike Senior Citizen Directory (one
    // entry per municipality), a person has no natural unique key, so two
    // rows sharing a region/position isn't necessarily a mistake.
    public class NcscTeamDirectoryPreviewResultDto
    {
        public List<NcscTeamDirectoryImportErrorDto> HardErrors { get; set; } = new();
        public int TotalRows { get; set; }
        public int CleanRows { get; set; }
    }

    public class NcscTeamDirectoryImportResultDto
    {
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int ImportedCount { get; set; }
        public int ErrorCount { get; set; }
        public bool HasErrors { get; set; }
        public bool IsSuccess { get; set; }
        public List<Guid> ImportedIds { get; set; } = new();
        public List<NcscTeamDirectoryImportErrorDto> Errors { get; set; } = new();
    }

    public class ImportNcscTeamDirectoryExcelSheetRequestDto
    {
        public IFormFile File { get; set; } = default!;
    }
}
