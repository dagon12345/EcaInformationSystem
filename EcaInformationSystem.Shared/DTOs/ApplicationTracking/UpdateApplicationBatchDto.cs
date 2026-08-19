namespace EcaInformationSystem.Shared.DTOs.ApplicationTracking
{
    // SuperAdmin-only override — edits the batch header regardless of who
    // currently holds it or what stage the workflow is on.
    public class UpdateApplicationBatchHeaderDto
    {
        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public int MilestoneYear { get; set; }
        public DateTime DateReceived { get; set; }

        // ApplicationPriority value (0=Normal, 1=Priority, 2=Urgent) — SuperAdmin
        // override, on top of whatever the sender originally set at creation.
        public int Priority { get; set; }
    }
}
