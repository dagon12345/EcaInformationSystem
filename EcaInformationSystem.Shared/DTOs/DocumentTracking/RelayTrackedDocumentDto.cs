namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    // Free-form relay — the current holder can send the document to any other user.
    public class RelayDocumentToDto
    {
        public Guid ToUserId { get; set; }
        public string? Note { get; set; }
        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
    }

    // Sends the document back to whoever handed it to the current holder — no
    // ToUserId, the target is derived from the holder's most recent inbound route.
    public class ReturnDocumentDto
    {
        public string? Note { get; set; }
        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
    }

    // Marks the document as done — the current holder stays the current holder.
    public class CompleteDocumentDto
    {
        public string? Note { get; set; }
    }

    // Creator (of the route)/SuperAdmin — correct a typo in an existing route
    // entry's Note/finding, without disturbing who it was sent to/from.
    public class UpdateDocumentRouteNoteDto
    {
        public string? Note { get; set; }
        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
    }
}
