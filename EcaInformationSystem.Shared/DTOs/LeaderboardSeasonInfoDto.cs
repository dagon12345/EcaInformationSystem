namespace EcaInformationSystem.Shared.DTOs
{
    // Powers the "Season N" header on the leaderboard card — the currently
    // active weekly season plus when it'll next auto-reset.
    public class LeaderboardSeasonInfoDto
    {
        public int SeasonNumber { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime NextAutoResetAtUtc { get; set; }
    }
}
