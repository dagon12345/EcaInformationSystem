namespace EcaInformationSystem.Shared.DTOs
{
    public class BulkUpdatePaymentStatusRequestDto
    {
        public List<Guid> Ids { get; set; } = new();
        public int PaymentStatus { get; set; }
        public int? ModeOfPayment { get; set; }
        public DateTime? PaymentDate { get; set; }
    }
}
