using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IVoiceCallLogService
    {
        // Called from VoiceCallHub — not user-facing.
        Task<Guid> CreateAsync(Guid callerId, Guid calleeId);
        Task EndAsync(Guid logId);

        // Logged the moment the callee explicitly rejects — never reaches
        // CreateAsync/EndAsync since the call never connected.
        Task LogDeclinedAsync(Guid callerId, Guid calleeId);

        // Logged when the caller ends the call while it's still ringing
        // (no one picked up) — same "never connected" shape as a decline.
        Task LogMissedAsync(Guid callerId, Guid calleeId);

        // Only the two participants of the call may write its notes.
        Task<VoiceCallLogDto> UpdateNotesAsync(Guid logId, Guid requestingUserId, string? notes);

        // SuperAdmin/Admin see everything; anyone else (PDO) sees only calls
        // they were a participant in.
        Task<List<VoiceCallLogDto>> GetForViewerAsync(Guid viewerId, string viewerRole);

        // SuperAdmin only — enforced again here, not just at the controller.
        Task DeleteAsync(Guid logId, string requestingRole);
    }
}
