// Shared/Helpers/NameSimilarityHelper.cs
public static class NameSimilarityHelper
{
    public static double ComputeNameSimilarity(string? a, string? b)
    {
        a = (a ?? string.Empty).Trim().ToUpperInvariant();
        b = (b ?? string.Empty).Trim().ToUpperInvariant();
        while (a.Contains("  ")) a = a.Replace("  ", " ");
        while (b.Contains("  ")) b = b.Replace("  ", " ");
        if (a.Length == 0 || b.Length == 0) return 0.0;
        if (a == b) return 1.0;
        int dist = LevenshteinDistance(a, b);
        return 1.0 - (double)dist / Math.Max(a.Length, b.Length);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        // ✅ Use a flat 1D array instead of 2D to avoid dimension miscalculation
        int aLen = a.Length;
        int bLen = b.Length;

        var prev = new int[bLen + 1];
        var curr = new int[bLen + 1];

        // Initialize first row: cost of deleting all chars from b
        for (int j = 0; j <= bLen; j++)
            prev[j] = j;

        for (int i = 1; i <= aLen; i++)
        {
            curr[0] = i; // cost of deleting i chars from a

            for (int j = 1; j <= bLen; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;

                curr[j] = Math.Min(
                    Math.Min(
                        prev[j] + 1,      // deletion
                        curr[j - 1] + 1), // insertion
                        prev[j - 1] + cost // substitution
                );
            }

            // Swap rows
            var temp = prev;
            prev = curr;
            curr = temp;
        }

        return prev[bLen];
    }
}