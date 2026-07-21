using EcaInformationSystem.Common.Enums;

namespace EcaInformationSystem.Domain.Entities
{
    public class Activity
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsAllDay { get; set; } = true;

        public ActivityType Type { get; set; }
        public ActivityPriority Priority { get; set; }

        public string? Location { get; set; }
        public string? PsgcCodeProvince { get; set; }
        public string? PsgcCodeMunicipality { get; set; }

        public bool IsCancelled { get; set; }
        public bool ReminderSent { get; set; }

        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? UpdatedByUserId { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsPublic { get; set; } = false;
    }
}