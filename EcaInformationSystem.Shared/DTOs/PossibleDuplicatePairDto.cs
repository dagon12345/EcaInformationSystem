namespace EcaInformationSystem.Shared.DTOs
{
    public class PossibleDuplicatePairDto
    {
        public Guid Record1Id { get; set; }
        public string Record1FullName { get; set; } = string.Empty;
        public string Record1BirthDate { get; set; } = string.Empty;
        public string Record1Municipality { get; set; }= string.Empty;
        public string Record1Barangay { get; set; } = string.Empty;
        public string Record1OscaId { get; set; } = string.Empty;
        public int Record1PaymentStatus { get; set; }

        public Guid Record2Id { get; set; }
        public string Record2FullName { get; set; } = string.Empty;
        public string Record2BirthDate { get; set; } = string.Empty;
        public string Record2Municipality { get; set; }= string.Empty;
        public string Record2Barangay { get; set; } = string.Empty;
        public string Record2OscaId { get; set; } = string.Empty;
        public int Record2PaymentStatus { get; set; }

        public double MatchScore { get; set; }
        public string MatchReason { get; set; } = string.Empty;
    }

    public class PossibleDuplicateSummaryDto
    {
        public int TotalPairs { get; set; }
        public List<PossibleDuplicatePairDto> Pairs { get; set; } = new();

        //Tells the uo the scan was cut short
        public bool TimedOut { get; set; }
        public string FilterDescription { get; set; } = string.Empty;
    }
}