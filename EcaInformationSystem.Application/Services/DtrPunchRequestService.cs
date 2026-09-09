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

        public async Task ApproveAsync(Guid requestId, string reviewedByName, string? notes)
        {
            var request = await _repo.GetByIdAsync(requestId);
            if (request is null || request.Status != DtrPunchRequestStatus.Pending)
                throw new InvalidOperationException("This request is no longer pending.");

            if (request.RequestType == DtrPunchRequestType.Add && request.RequestedPunchDateTime.HasValue)
            {
                await _attendanceLogService.AddManualPunchAsync(request.BiometricUserId, request.RequestedPunchDateTime.Value, request.RequestedByName);
            }
            else if (request.RequestType == DtrPunchRequestType.Remove && request.TargetLogId.HasValue)
            {
                await _attendanceLogService.RemovePunchAsync(request.TargetLogId.Value);
            }

            request.Status = DtrPunchRequestStatus.Approved;
            request.ReviewedByName = reviewedByName;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNotes = notes;
            await _repo.SaveChangesAsync();
        }

        public async Task RejectAsync(Guid requestId, string reviewedByName, string? notes)
        {
            var request = await _repo.GetByIdAsync(requestId);
            if (request is null || request.Status != DtrPunchRequestStatus.Pending)
                throw new InvalidOperationException("This request is no longer pending.");

            request.Status = DtrPunchRequestStatus.Rejected;
            request.ReviewedByName = reviewedByName;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNotes = notes;
            await _repo.SaveChangesAsync();
        }
    }
}
