namespace EcaInformationSystem.Shared.DTOs
{
    // One row per distinct BatchCode among CO Status = Endorsed (1) records —
    // backs the "View Endorsed Batches" button in GridView.razor.
    public class BatchCodeCoStatusSummaryDto
    {
        public string BatchCode { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
