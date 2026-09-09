namespace EcaInformationSystem.Domain.Entities
{
    public enum DtrPunchRequestType
    {
        Add = 0,
        Remove = 1
    }

    public enum DtrPunchRequestStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    // A non-Admin/Finance/SuperAdmin user's request to add or remove a punch
    // on their own DTR — unlike Admin/Finance/SuperAdmin (who mutate
    // AttendanceLog directly, see DtrController), everyone else's manual
    // punch edit lands here first and only takes effect once one of those
    // three roles approves it. "Editing" a punch (see DtrPrintDocument.razor's
    // EditManualPunch) is a Remove request for the old time plus an Add
    // request for the new one — two rows, not a single atomic edit — mirroring
    // how the direct-edit path already has no dedicated "update" endpoint
    // either (clear + re-add achieves the same result there too).
    public class DtrPunchEditRequest
    {
        public Guid Id { get; set; }

        // Whose DTR this affects (the requester, always — self-service only).
        public Guid UserId { get; set; }
        public string BiometricUserId { get; set; } = string.Empty;

        public DtrPunchRequestType RequestType { get; set; }

        // Remove only — the existing AttendanceLog.Id being asked to be cleared.
        public int? TargetLogId { get; set; }

        // Add only — the punch date/time being requested.
        public DateTime? RequestedPunchDateTime { get; set; }

        public DtrPunchRequestStatus Status { get; set; } = DtrPunchRequestStatus.Pending;

        public string RequestedByName { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }

        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }
    }
}
