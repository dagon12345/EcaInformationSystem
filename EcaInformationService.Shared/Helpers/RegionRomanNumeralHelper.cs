namespace EcaInformationSystem.Shared.Helpers
{
    public class RegionRomanNumeralHelper
    {
        //Philippine regions mapped to Roman numerals
        //Keys are PsgcCodeRegion values from Db
        private static readonly Dictionary<int, string> _map = new()
     {
        { 1400000000, "CAR" },    // Cordillera Administrative Region
        { 100000000,  "I"   },    // Ilocos Region
        { 200000000,  "II"  },    // Cagayan Valley
        { 300000000,  "III" },    // Central Luzon
        { 400000000,  "IV-A"},    // CALABARZON
        { 1700000000, "IV-B"},    // MIMAROPA
        { 500000000,  "V"   },    // Bicol Region
        { 600000000,  "VI"  },    // Western Visayas
        { 700000000,  "VII" },    // Central Visayas
        { 800000000,  "VIII"},    // Eastern Visayas
        { 900000000,  "IX"  },    // Zamboanga Peninsula
        { 1000000000, "X"   },    // Northern Mindanao
        { 1100000000, "XI"  },    // Davao Region
        { 1200000000, "XII" },    // SOCCSKSARGEN
        { 1300000000, "NCR" },    // National Capital Region
        { 1600000000, "XIII"},    // Caraga ← your default
        { 1900000000, "BARMM"},   // Bangsamoro
     };
        public static string GetRoman(int? psgcCodeRegion)
        {
            if (!psgcCodeRegion.HasValue) return "XIII"; // default Caraga
            return _map.TryGetValue(psgcCodeRegion.Value, out var roman) ? roman : "XIII";
        }
        public static string BuildReferenceNumber(int? quarter, string? batch, int? refYear, int? psgcCodeRegion)
        {
            if (!quarter.HasValue || string.IsNullOrWhiteSpace(batch) || !refYear.HasValue)
                return string.Empty;

            var roman = GetRoman(psgcCodeRegion);
            return $"Q{quarter}B{batch}-{refYear:D2}-{roman}";
            // Example: Q1B1-26-XIII
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