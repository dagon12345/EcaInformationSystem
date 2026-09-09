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

        // Approving actually performs the AttendanceLog mutation (Add or
        // Remove) that was deferred when the request was filed.
        Task ApproveAsync(Guid requestId, string reviewedByName, string? notes);
        Task RejectAsync(Guid requestId, string reviewedByName, string? notes);
    }
}
