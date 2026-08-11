using Microsoft.AspNetCore.Http;

namespace EcaInformationSystem.Shared.DTOs
{
    // High = most aggressive shrink (smallest output, lowest quality).
    // Low = mildest shrink (best quality, smallest size reduction).
    public enum ShrinkQuality
    {
        High,
        Medium,
        Low
    }

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

        // Set only on the response to an upload/replace that actually went
        // through the shrink pipeline — null means the file was stored as-is.
        public long? PreShrinkSizeBytes { get; set; }
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
        // Either File (direct upload — used when no shrink was needed) OR
        // PreviewToken (the file was already shrunk via /shrink-preview and its
        // result is cached server-side; no need to send the bytes again) must
        // be provided — never both.
        public IFormFile? File { get; set; }
        public Guid? PreviewToken { get; set; }

        public string Title { get; set; } = default!;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public Guid? FolderId { get; set; }

        // Required only when File is provided and its length exceeds the 10 MB
        // stored-size ceiling and the type is shrinkable (PDF/DOCX/XLSX) —
        // ignored when PreviewToken is used (the quality was already applied).
        public ShrinkQuality? ShrinkQuality { get; set; }
    }
    public class ReplaceFileRequest
    {
        public IFormFile File { get; set; } = default!;
        public ShrinkQuality? ShrinkQuality { get; set; }
    }

    // ── Shrink preview — lets the client show "estimated result: X MB" for a
    // chosen quality tier before the user commits to uploading. ─────────────
    public class ShrinkPreviewRequest
    {
        public IFormFile File { get; set; } = default!;
        public ShrinkQuality Quality { get; set; }
    }

    public class ShrinkPreviewResultDto
    {
        public Guid PreviewToken { get; set; }
        public long OriginalSizeBytes { get; set; }
        public long ShrunkSizeBytes { get; set; }
        public bool MeetsLimit { get; set; }
    }
}
