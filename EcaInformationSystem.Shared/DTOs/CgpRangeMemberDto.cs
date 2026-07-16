namespace EcaInformationSystem.Shared.DTOs
{
    public class CgpRangeMemberDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int? CgpPageNumber { get; set; }
        public int PaymentStatus { get; set; } // 0=N/A, 1=Unpaid, 2=Paid, 3=Pending
        public string OscaIdNumber { get; set; } = string.Empty;
        public DateTime? PaymentDate { get; set; }
        public int MilestoneYear { get; set; }
    }
}