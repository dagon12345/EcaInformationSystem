namespace EcaInformationSystem.Domain.Entities
{
    public class FormDocument
    {
        public Guid Id { get; set; }
        // ✅ NEW — null means "Uncategorized" / root level
        public Guid? FolderId { get; set; }
        public FormFolder? Folder { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }          // e.g. "COE", "Liquidation", "Payroll"

        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public byte[] FileData { get; set; } = Array.Empty<byte>();

        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // ✅ NEW — optional payroll tagging: setting these on a document is
        // what "links" it to matching grantees' payment history entries
        // (matched on PayrollQuarter + FiscalYear, GridView.razor). Clearing
        // them back to null is how an admin "unlinks" a wrongly-tagged file.
        // PsgcCodeRegion/PsgcCodeMunicipality are optional too — most existing
        // folders/documents have none, meaning "applies to every region" (or
        // every municipality within the tagged region) when matching. Payroll
        // is often run per-municipality even within one region, hence both.
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; }
        public int? PsgcCodeRegion { get; set; }
        public int? PsgcCodeProvince { get; set; }
        public int? PsgcCodeMunicipality { get; set; }

        public bool IsDeleted { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
