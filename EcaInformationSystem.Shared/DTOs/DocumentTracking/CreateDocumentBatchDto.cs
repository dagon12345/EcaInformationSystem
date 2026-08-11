namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    public class CreateDocumentBatchDto
    {
        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public int MilestoneYear { get; set; }
        public DateTime DateReceived { get; set; }

        public List<CreateDocumentGranteeRowDto> Rows { get; set; } = new();

        // Who the Viewer/Admin/SuperAdmin is initially endorsing this batch to.
        public Guid RecipientUserId { get; set; }
        public string? Note { get; set; }
        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
    }
}
