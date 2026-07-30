namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    // SuperAdmin-only override — edits the batch header regardless of who
    // currently holds it or what stage the workflow is on.
    public class UpdateDocumentBatchHeaderDto
    {
        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public int MilestoneYear { get; set; }
        public DateTime DateReceived { get; set; }
    }
}
