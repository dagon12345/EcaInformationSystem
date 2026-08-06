namespace EcaInformationSystem.Client.Services
{
    // Icon/color mapping for each transaction tier level — purely presentational,
    // kept in sync by hand with TransactionTierHelper.Tiers (Shared project).
    public static class TierVisuals
    {
        public record TierVisual(string Icon, string CssClass);

        private static readonly Dictionary<int, TierVisual> Map = new()
        {
            [1] = new("bi-seedling", "tier-rookie"),
            [2] = new("bi-award", "tier-bronze"),
            [3] = new("bi-award-fill", "tier-silver"),
            [4] = new("bi-trophy-fill", "tier-gold"),
            [5] = new("bi-gem", "tier-platinum"),
            [6] = new("bi-suit-diamond-fill", "tier-diamond"),
            [7] = new("bi-stars", "tier-master"),
            [8] = new("bi-fire", "tier-legend"),
            [9] = new("bi-crown-fill", "tier-god"),
        };

        public static TierVisual For(int tierLevel) =>
            Map.TryGetValue(tierLevel, out var v) ? v : Map[1];
    }
}
