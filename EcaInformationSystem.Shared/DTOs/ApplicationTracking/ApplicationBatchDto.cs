namespace EcaInformationSystem.Shared.DTOs.ApplicationTracking
{
    public class ApplicationBatchDto
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
        // The creator's account role at logging time (Viewer, PDO, Admin, or
        // SuperAdmin) — lets the client show "Return to PDO" instead of always
        // assuming "Return to Viewer" for the EndorsedByViewer -> Return step.
        public string? CreatedByRole { get; set; }
        public DateTime CreatedAt { get; set; }

        // Numeric ApplicationTrackingStatus value + a ready-to-render label, so the
        // client doesn't need its own copy of the enum to display status text.
        public int CurrentStatus { get; set; }
        public string CurrentStatusLabel { get; set; } = string.Empty;

        // Numeric ApplicationPriority value (0=Normal, 1=Priority, 2=Urgent) + a
        // ready-to-render label, same pattern as CurrentStatus/CurrentStatusLabel.
        public int Priority { get; set; }
        public string PriorityLabel { get; set; } = string.Empty;

        public Guid CurrentHolderUserId { get; set; }
        public string CurrentHolderName { get; set; } = string.Empty;
        public DateTime? CurrentLegAcceptedAt { get; set; }

        public int RowCount { get; set; }
        public int FindingCount { get; set; }

        public List<ApplicationGranteeRowDto> Rows { get; set; } = new();
        public List<ApplicationTransferDto> Transfers { get; set; } = new();
    }
}
