namespace EcaInformationSystem.Shared.DTOs
{
    public class ReactionSummaryDto
    {
        public int Like { get; set; }
        public int Wow { get; set; }
        public int Heart { get; set; }
        public int Confetti { get; set; }
        public int Total => Like + Wow + Heart + Confetti;
    }
}
