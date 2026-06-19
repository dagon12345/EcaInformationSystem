namespace EcaInformationSystem.Shared.Helpers
{
    public class RegionRomanNumeralHelper
    {
        private static readonly Dictionary<int, string> _map = new()
        {
            { 1400000000, "CAR"  },
            { 100000000,  "I"    },
            { 200000000,  "II"   },
            { 300000000,  "III"  },
            { 400000000,  "IV-A" },
            { 1700000000, "IV-B" },
            { 500000000,  "V"    },
            { 600000000,  "VI"   },
            { 700000000,  "VII"  },
            { 800000000,  "VIII" },
            { 900000000,  "IX"   },
            { 1000000000, "X"    },
            { 1100000000, "XI"   },
            { 1200000000, "XII"  },
            { 1300000000, "NCR"  },
            { 1600000000, "XIII" },
            { 1900000000, "BARMM"},
        };

        public static string GetRoman(int? psgcCodeRegion)
        {
            if (!psgcCodeRegion.HasValue) return "XIII";
            return _map.TryGetValue(psgcCodeRegion.Value, out var roman) ? roman : "XIII";
        }

        /// <summary>
        /// Generates a random 5-digit code (10000–99999).
        /// Call this ONCE at assignment time and store the result in RefCode.
        /// Never call this inside BuildReferenceNumber.
        /// </summary>
        public static string GenerateRefCode()
        {
            return Random.Shared.Next(10000, 99999).ToString();
        }

        /// <summary>
        /// Builds the reference number from stored components.
        /// RefCode must already be stored in the DB — pass it in directly.
        /// Example output: Q1B1-26-12345-XIII
        /// </summary>
        public static string BuildReferenceNumber(
            int? quarter,
            string? batch,
            int? refYear,
            string? refCode,
            int? psgcCodeRegion)
        {
            if (!quarter.HasValue
                || string.IsNullOrWhiteSpace(batch)
                || !refYear.HasValue
                || string.IsNullOrWhiteSpace(refCode))
                return string.Empty;

            var roman = GetRoman(psgcCodeRegion);
            return $"Q{quarter}B{batch}-{refYear:D2}-{refCode}-{roman}";
            // Example: Q1B1-26-12345-XIII
        }

        public static List<int> GetRegionCodesForRoman(string roman)
        {
            return _map
                .Where(x => x.Value.Equals(roman, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Key)
                .ToList();
        }
    }
}