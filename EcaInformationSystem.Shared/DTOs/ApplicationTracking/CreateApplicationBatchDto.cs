namespace EcaInformationSystem.Shared.DTOs.ApplicationTracking
{
    public class CreateApplicationBatchDto
    {
        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public int MilestoneYear { get; set; }
        public DateTime DateReceived { get; set; }

        // ApplicationPriority value (0=Normal, 1=Priority, 2=Urgent); defaults to Normal.
        public int Priority { get; set; }

        public List<CreateApplicationGranteeRowDto> Rows { get; set; } = new();

        // Who the Viewer/Admin/SuperAdmin is initially endorsing this batch to.
        public Guid RecipientUserId { get; set; }
        public string? Note { get; set; }
        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
    }
}
