namespace EcaInformationSystem.Shared.DTOs
{
    public class VoiceCallLogDto
    {
        public Guid Id { get; set; }
        public Guid CallerId { get; set; }
        public string CallerName { get; set; } = string.Empty;
        public Guid CalleeId { get; set; }
        public string CalleeName { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public int? DurationSeconds { get; set; }

        public string? Notes { get; set; }
        public DateTime? NotesUpdatedAt { get; set; }

        public int Status { get; set; } // 0=Completed, 1=Declined, 2=Missed/NotAnswered
        public string StatusLabel { get; set; } = string.Empty;

        public bool CanDelete { get; set; } // true only for SuperAdmin — decided server-side
    }

    public class UpdateVoiceCallLogNotesRequest
    {
        public string? Notes { get; set; }
    }
}
