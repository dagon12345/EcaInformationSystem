namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    // Creator/SuperAdmin — edits a tracked document's Title/Description/Status
    // without disturbing its current holder or routing history.
    public class UpdateTrackedDocumentDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        // TrackedDocumentStatus value (0=InTransit, 1=Completed) — a manual
        // override available to whoever can already edit the document (the
        // creator/sender or a SuperAdmin), independent of the Complete action.
        public int Status { get; set; }
    }
}
