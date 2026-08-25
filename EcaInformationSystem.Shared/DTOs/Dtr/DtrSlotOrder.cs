namespace EcaInformationSystem.Shared.DTOs.Dtr
{
    // The 4 punch columns in their fixed left-to-right order on the DTR
    // form — shared between server-side overlap validation
    // (DtrDayMarkService) and client-side range rendering (DtrCopy.razor),
    // so both agree on what "AmOut through PmOut" actually spans.
    public static class DtrSlotOrder
    {
        public static readonly string[] Slots = { "AmIn", "AmOut", "PmIn", "PmOut" };

        public static int IndexOf(string slot) => Array.IndexOf(Slots, slot);

        public static bool IsValidSlot(string? slot) => slot is null || IndexOf(slot) >= 0;

        // True if [aStart,aEnd] and [bStart,bEnd] (each inclusive, by slot
        // index) share at least one column.
        public static bool Overlaps(int aStart, int aEnd, int bStart, int bEnd) => aStart <= bEnd && bStart <= aEnd;
    }
}
