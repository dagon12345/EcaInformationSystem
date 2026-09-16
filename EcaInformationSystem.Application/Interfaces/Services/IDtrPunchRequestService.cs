using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IDtrPunchRequestService
    {
        // Files a new Add/Remove request for a non-privileged user's own DTR
        // and notifies every Admin/Finance/SuperAdmin in the requester's
        // region. Does NOT touch AttendanceLog — that only happens on approval.
        Task<Guid> RequestAddAsync(Guid userId, string biometricUserId, string employeeName, DateTime punchDateTime, string requestedByName, int? regionCode);
        Task<Guid> RequestRemoveAsync(Guid userId, string biometricUserId, string employeeName, int targetLogId, string currentPunchTime, DateTime punchDate, string requestedByName, int? regionCode);

        // Empty list for any role outside Admin/Finance/SuperAdmin — role
        // enforcement lives here, same as LivenessCheckService.GetPendingReviewsForUserAsync.
        Task<List<DtrPunchRequestDto>> GetPendingForRoleAsync(string role);

        // The CALLER's own pending requests, regardless of role — lets
        // MyDtr.razor show a "pending approval" marker on the specific days
        // that user is still waiting on, so they never lose track of what
        // they asked to correct.
        Task<List<DtrPunchRequestDto>> GetPendingForUserAsync(Guid userId);

        // Approving actually performs the AttendanceLog mutation (Add or
        // Remove) that was deferred when the request was filed.
        Task ApproveAsync(Guid requestId, string reviewedByName, string? notes);
        Task RejectAsync(Guid requestId, string reviewedByName, string? notes);

        // Bulk variants — approve/reject every id in one call (typically all
        // of one employee's pending requests at once) instead of one
        // round-trip per row. Ids that are missing or no longer pending are
        // skipped rather than failing the whole batch; returns how many were
        // actually applied.
        Task<int> ApproveManyAsync(List<Guid> requestIds, string reviewedByName, string? notes);
        Task<int> RejectManyAsync(List<Guid> requestIds, string reviewedByName, string? notes);
    }
}
