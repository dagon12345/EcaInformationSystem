// ============================================================
// NEW FILE: EcaInformationSystem.Shared/Extensions/JsonElementExtensions.cs
// ============================================================

using System.Text.Json;

namespace EcaInformationSystem.Shared.Extensions
{
    /// <summary>
    /// Helpers for safely reading DTO properties that may deserialize
    /// as JsonElement in Blazor WASM (System.Text.Json behavior).
    /// </summary>
    public static class JsonElementExtensions
    {
        public static string? ToSafeString(this object? value) => value switch
        {
            null => null,
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } je => je.GetString(),
            JsonElement { ValueKind: JsonValueKind.Number } je => je.GetRawText(),
            JsonElement { ValueKind: JsonValueKind.Null } _ => null,
            JsonElement { ValueKind: JsonValueKind.Undefined } _ => null,
            _ => value.ToString()
        };

        public static int? ToSafeInt(this object? value) => value switch
        {
            null => null,
            int i => i,
            long l => (int)l,
            JsonElement { ValueKind: JsonValueKind.Number } je
                when je.TryGetInt32(out var n) => n,
            JsonElement { ValueKind: JsonValueKind.Null } _ => null,
            _ => int.TryParse(value?.ToString(), out var p) ? p : null
        };

        public static bool ToSafeBool(this object? value, bool fallback = false) => value switch
        {
            null => fallback,
            bool b => b,
            JsonElement { ValueKind: JsonValueKind.True } _ => true,
            JsonElement { ValueKind: JsonValueKind.False } _ => false,
            _ => bool.TryParse(value?.ToString(), out var p) ? p : fallback
        };

        public static DateTime? ToSafeDateTime(this object? value) => value switch
        {
            null => null,
            DateTime dt => dt,
            JsonElement { ValueKind: JsonValueKind.String } je
                when je.TryGetDateTime(out var dt) => dt,
            JsonElement { ValueKind: JsonValueKind.Null } _ => null,
            _ => DateTime.TryParse(value?.ToString(), out var p) ? p : null
        };
    }
}