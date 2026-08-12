using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    // One row per browser-to-browser voice call (see VoiceCallHub) — created
    // the moment a call is accepted, finalized when it ends. Notes are always
    // optional; an unfilled call still leaves a permanent, undeletable-by-
    // default audit trail of who called whom and when.
    public class VoiceCallLog
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid CallerId { get; set; }
        [MaxLength(200)]
        public string CallerName { get; set; } = string.Empty;

        [Required]
        public Guid CalleeId { get; set; }
        [MaxLength(200)]
        public string CalleeName { get; set; } = string.Empty;

        [Required]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }
        public int? DurationSeconds { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
        public DateTime? NotesUpdatedAt { get; set; }

        // 0 = Completed (was accepted and connected), 1 = Declined (callee
        // rejected), 2 = Missed/NotAnswered (caller ended it before it was
        // accepted). Declined/Missed rows never get an EndedAt/Duration —
        // the call never actually connected.
        public int Status { get; set; }
    }
}
