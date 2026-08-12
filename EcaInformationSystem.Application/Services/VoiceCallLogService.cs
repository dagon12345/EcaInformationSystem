using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    // Audit trail for browser-to-browser voice calls (VoiceCallHub). A log
    // row is created the moment a call is accepted and finalized when it
    // ends, regardless of whether anyone ever adds notes — "no notes" is a
    // valid, permanent record, not an incomplete one.
    public class VoiceCallLogService : IVoiceCallLogService
    {
        private static class CallStatus
        {
            public const int Completed = 0;
            public const int Declined = 1;
            public const int Missed = 2;
        }

        private readonly IVoiceCallLogRepository _repo;
        private readonly IPendingUserRegistrationRepository _userRepo;

        public VoiceCallLogService(IVoiceCallLogRepository repo, IPendingUserRegistrationRepository userRepo)
        {
            _repo = repo;
            _userRepo = userRepo;
        }

        public async Task<Guid> CreateAsync(Guid callerId, Guid calleeId)
        {
            var caller = await _userRepo.GetByIdAsync(callerId);
            var callee = await _userRepo.GetByIdAsync(calleeId);

            var log = new VoiceCallLog
            {
                Id = Guid.NewGuid(),
                CallerId = callerId,
                CallerName = caller?.FullName ?? "Unknown",
                CalleeId = calleeId,
                CalleeName = callee?.FullName ?? "Unknown",
                StartedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(log);
            await _repo.SaveChangesAsync();
            return log.Id;
        }

        public async Task EndAsync(Guid logId)
        {
            var log = await _repo.GetByIdAsync(logId);
            if (log is null || log.EndedAt.HasValue) return; // already finalized — idempotent

            log.EndedAt = DateTime.UtcNow;
            log.DurationSeconds = (int)(log.EndedAt.Value - log.StartedAt).TotalSeconds;
            await _repo.SaveChangesAsync();
        }

        public Task LogDeclinedAsync(Guid callerId, Guid calleeId) => LogUnconnectedAsync(callerId, calleeId, CallStatus.Declined);

        public Task LogMissedAsync(Guid callerId, Guid calleeId) => LogUnconnectedAsync(callerId, calleeId, CallStatus.Missed);

        private async Task LogUnconnectedAsync(Guid callerId, Guid calleeId, int status)
        {
            var caller = await _userRepo.GetByIdAsync(callerId);
            var callee = await _userRepo.GetByIdAsync(calleeId);

            var log = new VoiceCallLog
            {
                Id = Guid.NewGuid(),
                CallerId = callerId,
                CallerName = caller?.FullName ?? "Unknown",
                CalleeId = calleeId,
                CalleeName = callee?.FullName ?? "Unknown",
                StartedAt = DateTime.UtcNow,
                Status = status
            };

            await _repo.AddAsync(log);
            await _repo.SaveChangesAsync();
        }

        public async Task<VoiceCallLogDto> UpdateNotesAsync(Guid logId, Guid requestingUserId, string? notes)
        {
            var log = await _repo.GetByIdAsync(logId)
                ?? throw new KeyNotFoundException("Call log not found.");

            if (log.CallerId != requestingUserId && log.CalleeId != requestingUserId)
                throw new UnauthorizedAccessException("You weren't a participant on this call.");

            log.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            log.NotesUpdatedAt = DateTime.UtcNow;
            await _repo.SaveChangesAsync();

            return ToDto(log, canDelete: false);
        }

        public async Task<List<VoiceCallLogDto>> GetForViewerAsync(Guid viewerId, string viewerRole)
        {
            // Only SuperAdmin sees every call. Everyone else — Admin, PDO,
            // Focal alike — sees only calls they were personally a
            // participant in.
            var isSuperAdmin = viewerRole == "SuperAdmin";

            var logs = isSuperAdmin
                ? await _repo.GetAllAsync()
                : await _repo.GetForUserAsync(viewerId);

            return logs.Select(l => ToDto(l, canDelete: isSuperAdmin)).ToList();
        }

        public async Task DeleteAsync(Guid logId, string requestingRole)
        {
            if (requestingRole != "SuperAdmin")
                throw new UnauthorizedAccessException("Only a Super Administrator can delete call logs.");

            var log = await _repo.GetByIdAsync(logId);
            if (log is null) return; // already gone — idempotent

            _repo.Remove(log);
            await _repo.SaveChangesAsync();
        }

        private static VoiceCallLogDto ToDto(VoiceCallLog log, bool canDelete) => new()
        {
            Id = log.Id,
            CallerId = log.CallerId,
            CallerName = log.CallerName,
            CalleeId = log.CalleeId,
            CalleeName = log.CalleeName,
            StartedAt = log.StartedAt,
            EndedAt = log.EndedAt,
            DurationSeconds = log.DurationSeconds,
            Notes = log.Notes,
            NotesUpdatedAt = log.NotesUpdatedAt,
            Status = log.Status,
            StatusLabel = log.Status switch
            {
                CallStatus.Declined => "Declined",
                CallStatus.Missed => "Not Answered",
                _ => "Completed"
            },
            CanDelete = canDelete
        };
    }
}
