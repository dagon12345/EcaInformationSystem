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

    // Pushed over SignalR whenever a new punch-edit request needs review.
    public class DtrPunchRequestSubmittedNotificationDto
    {
        public Guid RequestId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string RequestType { get; set; } = string.Empty;
        public DateTime PunchDate { get; set; }
        public DateTime RequestedAt { get; set; }
    }
}
