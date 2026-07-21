using EcaInformationSystem.Common.Enums;

namespace EcaInformationSystem.Shared.DTOs.Activity
{
    public class ActivityDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsAllDay { get; set; }
        public ActivityType Type { get; set; }
        public ActivityPriority Priority { get; set; }
        public string? Location { get; set; }
        public string? PsgcCodeProvince { get; set; }
        public string? PsgcCodeMunicipality { get; set; }
        public bool IsCancelled { get; set; }
        public bool IsPublic { get; set; }
    }

    public class ActivityUpsertDto
    {
        public int? Id { get; set; }
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
        public bool IsPublic { get; set; }
    }

    public class ActivityMonthMarkerDto
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public List<ActivityPriority> Priorities { get; set; } = new();
        public List<ActivitySpanSegmentDto> Segments { get; set; } = new();
    }

    public class ActivitySpanSegmentDto
    {
        public int ActivityId { get; set; }
        public string Title { get; set; } = string.Empty;
        public ActivityPriority Priority { get; set; }
        public bool IsRangeStart { get; set; }
        public bool IsRangeEnd { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }
        public bool IsAllDay { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsPublic { get; set; }
    }
}