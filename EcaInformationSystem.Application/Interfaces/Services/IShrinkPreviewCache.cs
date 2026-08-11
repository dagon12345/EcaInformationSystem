namespace EcaInformationSystem.Application.Interfaces.Services
{
    public class ShrinkPreviewEntry
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long OriginalSizeBytes { get; set; }
    }

    // Holds an already-shrunk file's bytes between /shrink-preview and /upload
    // so a large file only ever has to cross the wire once — the preview call
    // does the (expensive) shrink and caches the result; the confirming upload
    // just references it by token instead of re-sending/re-shrinking the file.
    public interface IShrinkPreviewCache
    {
        Guid Store(ShrinkPreviewEntry entry);

        // One-shot: removes the entry once read, so an upload can't be replayed
        // against a stale cached result.
        ShrinkPreviewEntry? Take(Guid token);
    }
}
