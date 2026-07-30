namespace EcaInformationSystem.Domain.Common.Enum
{
    // The workflow "leg" a document batch is currently on. Each status has an
    // implicit pending/accepted sub-state (see DocumentBatch.CurrentLegAcceptedAt) —
    // the tagged CurrentHolderUserId must accept before they can advance the batch
    // to the next leg.
    public enum DocumentTrackingStatus
    {
        EndorsedByViewer = 0,               // Viewer -> tagged recipient
        ReturnedToViewer = 1,                // Recipient -> the viewer who created the batch
        DistributedToPdo = 2,                // Viewer -> tagged PDO
        EndorsedToFinance = 3,               // PDO -> tagged Finance user
        ReturnedToPdoForFindings = 4,        // Finance -> tagged PDO (repeatable loop with EndorsedToFinance)
        ForwardedToViewerForScanning = 5,    // Finance -> tagged Viewer, final handoff
        Completed = 6                        // Viewer has scanned — terminal state
    }
}
