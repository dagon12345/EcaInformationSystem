namespace EcaInformationSystem.Shared.DTOs
{
    public class ReactionResultDto
    {
        public int? ViewerReactionType { get; set; }
        public ReactionSummaryDto Reactions { get; set; } = new();
    }
}
