using Microsoft.AspNetCore.Http;

namespace EcaInformationSystem.Shared.DTOs
{
    // Metadata only — used for the listing grid. Never carries FileData.
    public class FormDocumentDto
    {
        public Guid Id { get; set; }
        public Guid? FolderId { get; set; }          // ✅ NEW
        public string? FolderName { get; set; }       // ✅ NEW
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class FormDocumentUpdateDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public Guid? FolderId { get; set; }
    }
    // ✅ NEW — search/filter payload
    public class FormDocumentSearchDto
    {
        public string? SearchTerm { get; set; }   // matches Title, Description, OriginalFileName, Category
        public Guid? FolderId { get; set; }       // null = all folders; use Guid.Empty sentinel for "Uncategorized" if you want that too
        public bool UncategorizedOnly { get; set; } = false;

        // "Date" (UploadedAt) or "Name" (Title). Defaults to ascending.
        public string SortBy { get; set; } = "Date";
        public bool SortAscending { get; set; } = true;
    }
    public class UploadFormDocumentRequest
    {
        public IFormFile File { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public Guid? FolderId { get; set; }
    }
    public class ReplaceFileRequest
    {
        public IFormFile File { get; set; } = default!;
    }
}
