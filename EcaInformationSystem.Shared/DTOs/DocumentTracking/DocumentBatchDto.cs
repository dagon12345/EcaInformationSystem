namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    public class DocumentBatchDto
    {
        public Guid Id { get; set; }

        public int PsgcCodeProvince { get; set; }
        public string ProvinceName { get; set; } = string.Empty;
        public int PsgcCodeMunicipality { get; set; }
        public string MunicipalityName { get; set; } = string.Empty;
        public int MilestoneYear { get; set; }
        public DateTime DateReceived { get; set; }

        public Guid CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Numeric DocumentTrackingStatus value + a ready-to-render label, so the
        // client doesn't need its own copy of the enum to display status text.
        public int CurrentStatus { get; set; }
        public string CurrentStatusLabel { get; set; } = string.Empty;

        public Guid CurrentHolderUserId { get; set; }
        public string CurrentHolderName { get; set; } = string.Empty;
        public DateTime? CurrentLegAcceptedAt { get; set; }

        public int RowCount { get; set; }
        public int FindingCount { get; set; }

        public List<DocumentGranteeRowDto> Rows { get; set; } = new();
        public List<DocumentTransferDto> Transfers { get; set; } = new();
    }
}
