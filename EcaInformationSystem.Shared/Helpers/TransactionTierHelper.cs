namespace EcaInformationSystem.Shared.Helpers
{
    // Gamified "how much have you actually done in this system" ladder — driven
    // by the count of Log entries a user has generated (excluding Category
    // "Login", which just tracks sign-ins, not work). Levels/thresholds and
    // naming are a deliberate design choice, not derived from anything else.
    public static class TransactionTierHelper
    {
        public record TierDefinition(int Level, string Name, int MinCount);

        public static readonly List<TierDefinition> Tiers = new()
        {
            new(1, "Rookie",   0),
            new(2, "Bronze",   25),
            new(3, "Silver",   250),
            new(4, "Gold",     1000),
            new(5, "Platinum", 5000),
            new(6, "Diamond",  15000),
            new(7, "Master",   25000),
            new(8, "Legend",   35000),
            new(9, "God Tier", 50000),
        };

        public static TierDefinition GetCurrentTier(int transactionCount) =>
            Tiers.Last(t => transactionCount >= t.MinCount);

        public static TierDefinition? GetNextTier(int transactionCount)
        {
            var current = GetCurrentTier(transactionCount);
            return Tiers.FirstOrDefault(t => t.MinCount > current.MinCount);
        }

        // 0-100, or 100 flat once the max tier is reached (nothing left to climb toward).
        public static double GetProgressPercent(int transactionCount)
        {
            var current = GetCurrentTier(transactionCount);
            var next = GetNextTier(transactionCount);
            if (next is null) return 100;

            var span = next.MinCount - current.MinCount;
            if (span <= 0) return 100;

            var into = transactionCount - current.MinCount;
            return Math.Clamp(into * 100.0 / span, 0, 100);
        }
    }
}
