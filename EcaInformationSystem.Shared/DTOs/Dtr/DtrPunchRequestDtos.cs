namespace EcaInformationSystem.Shared.DTOs.Dtr
{
    // Returned by manual-punch/manual-punch/{id} instead of the usual direct-
    // mutation response when the caller isn't Admin/Finance/SuperAdmin —
    // Pending=true tells the client the punch wasn't actually written yet, it
    // was filed as a request awaiting approval.
    public class DtrPunchActionResultDto
    {
        public bool Pending { get; set; }
        public string? Message { get; set; }
        public int? Id { get; set; }
    }

    // One pending request, as shown in the Admin/Finance/SuperAdmin
    // notification bell and the approval list.
    public class DtrPunchRequestDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string RequestType { get; set; } = string.Empty; // "Add" | "Remove"
        public DateTime PunchDate { get; set; }

        // Remove only — the punch time being asked to be cleared, formatted for display.
        public string? CurrentPunchTime { get; set; }

        // Add only — the punch time being requested.
        public string? RequestedPunchTime { get; set; }

        public string RequestedByName { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
    }

    public class ReviewDtrPunchRequestDto
    {
        public string? Notes { get; set; }
    }

    // Approve/reject several requests (typically every pending request for
    // one employee) in one call instead of one round-trip per row.
    public class BulkReviewDtrPunchRequestDto
    {
        public List<Guid> Ids { get; set; } = new();
        public string? Notes { get; set; }
    }

    // Pushed over SignalR whenever a new punch-edit request needs review.
    public class DtrPunchRequestSubmittedNotificationDto
    {
        public Guid RequestId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string RequestType { get; set; } = string.Empty;
        public DateTime PunchDate { get; set; }
        public DateTime RequestedAt { get; set; }
    }

    // Pushed over SignalR to the ORIGINAL REQUESTER (not the approvers) once
    // their Add/Remove request has been approved or rejected, so the bell
    // that already exists on every layout can surface it without polling.
    public class DtrPunchRequestDecidedNotificationDto
    {
        public Guid RequestId { get; set; }
        public string RequestType { get; set; } = string.Empty; // "Add" | "Remove"
        public DateTime PunchDate { get; set; }
        public string Decision { get; set; } = string.Empty; // "Approved" | "Rejected"
        public string ReviewedByName { get; set; } = string.Empty;
        public DateTime DecidedAt { get; set; }
    }
}
