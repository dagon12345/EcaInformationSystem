using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Services
{
    // Self-service DTR punch edits from anyone who isn't Admin/Finance/
    // SuperAdmin land here as a Pending request instead of writing straight
    // to AttendanceLog — see DtrController.AddManualPunch/RemoveManualPunch
    // for the role branch that decides which path a given caller takes.
    public class DtrPunchRequestService : IDtrPunchRequestService
    {
        // Same three roles allowed to edit/approve directly — kept in one
        // place so the "who can approve" set can't drift from "who bypasses
        // the request flow" in DtrController.
        public static readonly HashSet<string> ApproverRoles = new() { "Admin", "Finance", "SuperAdmin" };

        private readonly IDtrPunchRequestRepository _repo;
        private readonly IAttendanceLogService _attendanceLogService;
        private readonly IPendingUserRegistrationRepository _userRepo;
        private readonly IDtrPunchRequestBroadcaster _broadcaster;

        public DtrPunchRequestService(
            IDtrPunchRequestRepository repo,
            IAttendanceLogService attendanceLogService,
            IPendingUserRegistrationRepository userRepo,
            IDtrPunchRequestBroadcaster broadcaster)
        {
            _repo = repo;
            _attendanceLogService = attendanceLogService;
            _userRepo = userRepo;
            _broadcaster = broadcaster;
        }

        public async Task<Guid> RequestAddAsync(Guid userId, string biometricUserId, string employeeName, DateTime punchDateTime, string requestedByName, int? regionCode)
        {
            var request = await _repo.AddAsync(new DtrPunchEditRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BiometricUserId = biometricUserId,
                RequestType = DtrPunchRequestType.Add,
                RequestedPunchDateTime = punchDateTime,
                Status = DtrPunchRequestStatus.Pending,
                RequestedByName = requestedByName,
                RequestedAt = DateTime.UtcNow
            });
            await _repo.SaveChangesAsync();

            await NotifyApproversAsync(request.Id, employeeName, "Add", punchDateTime, regionCode);
            return request.Id;
        }

        public async Task<Guid> RequestRemoveAsync(Guid userId, string biometricUserId, string employeeName, int targetLogId, string currentPunchTime, DateTime punchDate, string requestedByName, int? regionCode)
        {
            var request = await _repo.AddAsync(new DtrPunchEditRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BiometricUserId = biometricUserId,
                RequestType = DtrPunchRequestType.Remove,
                TargetLogId = targetLogId,
                Status = DtrPunchRequestStatus.Pending,
                RequestedByName = requestedByName,
                RequestedAt = DateTime.UtcNow
            });
            await _repo.SaveChangesAsync();

            await NotifyApproversAsync(request.Id, employeeName, "Remove", punchDate, regionCode);
            return request.Id;
        }

        private async Task NotifyApproversAsync(Guid requestId, string employeeName, string requestType, DateTime punchDate, int? regionCode)
        {
            var targetUserIds = await ResolveApproverUserIdsAsync(regionCode);
            if (targetUserIds.Count == 0) return;

            await _broadcaster.NotifyPunchRequestSubmittedAsync(targetUserIds, new DtrPunchRequestSubmittedNotificationDto
            {
                RequestId = requestId,
                EmployeeName = employeeName,
                RequestType = requestType,
                PunchDate = punchDate,
                RequestedAt = DateTime.UtcNow
            });
        }

        private async Task<List<Guid>> ResolveApproverUserIdsAsync(int? regionCode)
        {
            var allUsers = await _userRepo.GetAllAsync();
            return allUsers
                .Where(u => u.ApprovalStatus == (int)ApprovalStatus.Approved && u.IsActivated && !u.IsDeactivated)
                .Where(u => ApproverRoles.Contains(u.Role))
                .Where(u => regionCode is null || u.Region == regionCode)
                .Select(u => u.Id)
                .Distinct()
                .ToList();
        }

        public async Task<List<DtrPunchRequestDto>> GetPendingForRoleAsync(string role)
        {
            if (!ApproverRoles.Contains(role)) return new();

            var rows = await _repo.GetPendingAsync();
            var userIds = rows.Select(r => r.UserId).Distinct().ToList();
            var users = await _userRepo.GetAllAsync();
            var namesById = users.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => u.FullName);

            var results = new List<DtrPunchRequestDto>();
            foreach (var r in rows)
            {
                DateTime? currentPunchTime = null;
                if (r.RequestType == DtrPunchRequestType.Remove && r.TargetLogId.HasValue)
                    currentPunchTime = await _attendanceLogService.GetPunchTimeAsync(r.TargetLogId.Value);

                results.Add(new DtrPunchRequestDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    EmployeeName = namesById.TryGetValue(r.UserId, out var name) ? name : "Unknown",
                    RequestType = r.RequestType.ToString(),
                    PunchDate = (r.RequestedPunchDateTime ?? currentPunchTime ?? r.RequestedAt).Date,
                    CurrentPunchTime = currentPunchTime?.ToString("h:mm tt"),
                    RequestedPunchTime = r.RequestedPunchDateTime?.ToString("h:mm tt"),
                    RequestedByName = r.RequestedByName,
                    RequestedAt = r.RequestedAt
                });
            }

            return results.OrderByDescending(r => r.RequestedAt).ToList();
        }

        // The caller's own pending requests, regardless of role — no
        // ApproverRoles gate here, everyone is allowed to see their own.
        public async Task<List<DtrPunchRequestDto>> GetPendingForUserAsync(Guid userId)
        {
            var rows = await _repo.GetPendingForUserAsync(userId);
            var user = await _userRepo.GetByIdAsync(userId);
            var employeeName = user?.FullName ?? "Unknown";

            var results = new List<DtrPunchRequestDto>();
            foreach (var r in rows)
            {
                DateTime? currentPunchTime = null;
                if (r.RequestType == DtrPunchRequestType.Remove && r.TargetLogId.HasValue)
                    currentPunchTime = await _attendanceLogService.GetPunchTimeAsync(r.TargetLogId.Value);

                results.Add(new DtrPunchRequestDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    EmployeeName = employeeName,
                    RequestType = r.RequestType.ToString(),
                    PunchDate = (r.RequestedPunchDateTime ?? currentPunchTime ?? r.RequestedAt).Date,
                    CurrentPunchTime = currentPunchTime?.ToString("h:mm tt"),
                    RequestedPunchTime = r.RequestedPunchDateTime?.ToString("h:mm tt"),
                    RequestedByName = r.RequestedByName,
                    RequestedAt = r.RequestedAt
                });
            }

            return results.OrderByDescending(r => r.RequestedAt).ToList();
        }

        public async Task ApproveAsync(Guid requestId, string reviewedByName, string? notes)
        {
            var request = await _repo.GetByIdAsync(requestId);
            if (request is null || !await TryApplyApprovalAsync(request, reviewedByName, notes))
                throw new InvalidOperationException("This request is no longer pending.");

            await _repo.SaveChangesAsync();
        }

        public async Task RejectAsync(Guid requestId, string reviewedByName, string? notes)
        {
            var request = await _repo.GetByIdAsync(requestId);
            if (request is null || !await TryApplyRejectionAsync(request, reviewedByName, notes))
                throw new InvalidOperationException("This request is no longer pending.");

            await _repo.SaveChangesAsync();
        }

        // Bulk approve/reject — same per-request rules as the single-id
        // methods above, but ids that are missing or already reviewed are
        // silently skipped instead of aborting the whole batch, and every
        // row is saved together in one round-trip.
        public async Task<int> ApproveManyAsync(List<Guid> requestIds, string reviewedByName, string? notes)
        {
            var applied = 0;
            foreach (var id in requestIds)
            {
                var request = await _repo.GetByIdAsync(id);
                if (request is not null && await TryApplyApprovalAsync(request, reviewedByName, notes))
                    applied++;
            }
            await _repo.SaveChangesAsync();
            return applied;
        }

        public async Task<int> RejectManyAsync(List<Guid> requestIds, string reviewedByName, string? notes)
        {
            var applied = 0;
            foreach (var id in requestIds)
            {
                var request = await _repo.GetByIdAsync(id);
                if (request is not null && await TryApplyRejectionAsync(request, reviewedByName, notes))
                    applied++;
            }
            await _repo.SaveChangesAsync();
            return applied;
        }

        private async Task<bool> TryApplyApprovalAsync(DtrPunchEditRequest request, string reviewedByName, string? notes)
        {
            if (request.Status != DtrPunchRequestStatus.Pending) return false;

            var punchDate = request.RequestedPunchDateTime;

            if (request.RequestType == DtrPunchRequestType.Add && request.RequestedPunchDateTime.HasValue)
            {
                await _attendanceLogService.AddManualPunchAsync(request.BiometricUserId, request.RequestedPunchDateTime.Value, request.RequestedByName);
            }
            else if (request.RequestType == DtrPunchRequestType.Remove && request.TargetLogId.HasValue)
            {
                // Look the punch time up BEFORE removing it — the requester's
                // "Approved" notification wants to show which time this was.
                punchDate = await _attendanceLogService.GetPunchTimeAsync(request.TargetLogId.Value);
                await _attendanceLogService.RemovePunchAsync(request.TargetLogId.Value);
            }

            request.Status = DtrPunchRequestStatus.Approved;
            request.ReviewedByName = reviewedByName;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNotes = notes;

            await NotifyRequesterAsync(request, "Approved", punchDate);
            return true;
        }

        private async Task<bool> TryApplyRejectionAsync(DtrPunchEditRequest request, string reviewedByName, string? notes)
        {
            if (request.Status != DtrPunchRequestStatus.Pending) return false;

            var punchDate = request.RequestedPunchDateTime;
            if (request.RequestType == DtrPunchRequestType.Remove && request.TargetLogId.HasValue)
                punchDate = await _attendanceLogService.GetPunchTimeAsync(request.TargetLogId.Value);

            request.Status = DtrPunchRequestStatus.Rejected;
            request.ReviewedByName = reviewedByName;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNotes = notes;

            await NotifyRequesterAsync(request, "Rejected", punchDate);
            return true;
        }

        // Pushes the decision back to whoever originally filed the request —
        // separate from NotifyApproversAsync above, which fans out to every
        // Admin/Finance/SuperAdmin instead of one specific user.
        private async Task NotifyRequesterAsync(DtrPunchEditRequest request, string decision, DateTime? punchDate)
        {
            await _broadcaster.NotifyPunchRequestDecidedAsync(request.UserId, new DtrPunchRequestDecidedNotificationDto
            {
                RequestId = request.Id,
                RequestType = request.RequestType.ToString(),
                PunchDate = (punchDate ?? request.RequestedAt).Date,
                Decision = decision,
                ReviewedByName = request.ReviewedByName ?? "Unknown",
                DecidedAt = request.ReviewedAt ?? DateTime.UtcNow
            });
        }
    }
}
