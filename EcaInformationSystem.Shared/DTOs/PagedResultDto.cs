namespace EcaInformationSystem.Shared.DTOs
{
    public class PagedResultDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public bool IsFuzzyMatch { get; set; }

        // Only set (non-zero) by GetPagedListAsync when BeneficiaryFilterDto.
        // IncludeKnownDuplicates is false (the default) — how many records the
        // filter would otherwise have matched, but were excluded because
        // they're the newer side of a Known Duplicate pair. Lets the grid show
        // "N Known Duplicates hidden — Show" without a second round-trip.
        public int HiddenKnownDuplicateCount { get; set; }

        public int TotalPages =>
            PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
