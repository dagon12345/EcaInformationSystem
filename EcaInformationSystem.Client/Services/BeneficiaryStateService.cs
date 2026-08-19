using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Net.Http.Json;

public class BeneficiaryStateService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _memoryCache;
    // ✅ UPDATE CONSTRUCTOR
    public BeneficiaryStateService(HttpClient http, IMemoryCache memoryCache)
    {
        _http = http;
        _memoryCache = memoryCache;
    }

    public bool HasActiveFilter { get; private set; } = false;

    // ✅ Persist filter UI selections across navigation
    public int SelectedRegionId { get; set; } = 0;
    public List<int> SelectedProvinceIds { get; set; } = new();
    public List<int> SelectedMunicipalityIds { get; set; } = new();
    public int SelectedBarangayId { get; set; } = 0;
    public int SelectedSex { get; set; }
    public List<int> SelectedPaymentStatuses { get; set; } = new();
    public List<int> SelectedPayrollQuarters { get; set; } = new(); // ✅ new
    public string? ErrorMessage { get; set; }

    // ✅ Persist loaded dropdown lists so they don't reload on back-navigation
    public List<RegionLookupDto> FilterRegions { get; set; } = new();
    public List<ProvinceLookupDto> FilterProvinces { get; set; } = new();
    public List<MunicipalityLookupDto> FilterMunicipalities { get; set; } = new();

    public List<BarangayLookupDto> FilterBarangays { get; set; } = new();

    // Shared Data
    public BeneficiaryFilterDto Filter { get; set; } = new()
    {
        PageNumber = 1,
        PageSize = 10
    };

    public List<BeneficiaryListItemDto> Beneficiaries { get; private set; } = new();
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public bool IsLoading { get; private set; }


    // ✅ Add these two
    public DateTime? LastLoaded { get; private set; }
    // ── Fuzzy "Search Similar Names" state ─────────────────────────────────────
    public bool LastResultWasFuzzy { get; private set; }

    // ✅ NEW — last crossmatch scan result, kept here (not inside the modal
    // component) so it survives closing the modal, searching/filtering the
    // grid, and navigating to other pages — same pattern as the rest of this
    // service. Cleared only when a new scan is run or the user explicitly
    // dismisses it, so the user never has to re-scan or re-download just to
    // pick up where they left off reviewing.
    public CrossmatchResultDto? LastCrossmatchResult { get; private set; }
    public string? LastCrossmatchFileName { get; private set; }
    public DateTime? LastCrossmatchAt { get; private set; }

    public void SetLastCrossmatch(CrossmatchResultDto result, string? fileName)
    {
        LastCrossmatchResult = result;
        LastCrossmatchFileName = fileName;
        LastCrossmatchAt = DateTime.Now;
        NotifyStateChanged();
    }

    public void ClearLastCrossmatch()
    {
        LastCrossmatchResult = null;
        LastCrossmatchFileName = null;
        LastCrossmatchAt = null;
        NotifyStateChanged();
    }
    public void SetBeneficiaries(List<BeneficiaryListItemDto> items)
    {
        Beneficiaries = items;
        NotifyStateChanged();
    }
    public void Invalidate() => LastLoaded = null; // ✅ forces reload on next visit

    public bool IsStale() =>
        LastLoaded == null ||
        (DateTime.Now - LastLoaded.Value).TotalSeconds > 30; // ✅ also catches tab switches


    // Event to notify components of changes
    public event Action? OnChange;

    // ✅ Add this method
    public void ClearData()
    {
        Beneficiaries = new List<BeneficiaryListItemDto>();
        TotalCount = 0;
        TotalPages = 0;
        LastLoaded = null;
        HasActiveFilter = false; // ✅ reset on clear
        NotifyStateChanged();
    }
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            var response = await _http.PostAsJsonAsync("api/beneficiary/paged-list", Filter);
            var result = await response.Content.ReadFromJsonAsync<PagedResultDto<BeneficiaryListItemDto>>();

            if (response.IsSuccessStatusCode)
            {
                Beneficiaries = result?.Items ?? new List<BeneficiaryListItemDto>();
                TotalCount = result?.TotalCount ?? 0;
                TotalPages = result?.TotalPages ?? 0;
                LastResultWasFuzzy = false;

                // ✅ Auto-fallback — if the exact search found nothing AND the
                // person searched by name, automatically try the fuzzy match
                // instead of making them click a separate button.
                //
                // ⚠️ SearchSimilarNamesAsync matches on name ONLY — it ignores every
                // other filter (ReplacementStatus, CoStatus, region, etc). If another
                // filter is also active, a correctly-empty result (e.g. "this person
                // matches the name but not this status") must NOT be silently replaced
                // by a fuzzy name match that ignores that status. Only fall back when
                // the search is effectively name-only.
                var nameTerm = !string.IsNullOrWhiteSpace(Filter.FullName) ? Filter.FullName
                    : !string.IsNullOrWhiteSpace(Filter.LastName) ? Filter.LastName
                    : !string.IsNullOrWhiteSpace(Filter.FirstName) ? Filter.FirstName
                    : Filter.GeneralSearch;

                if (TotalCount == 0 && !string.IsNullOrWhiteSpace(nameTerm) && !HasNonNameFilters())
                {
                    await TryFuzzyFallbackAsync();
                }
            }
            else
            {
                ApiErrorResponse? errorBody = null;
                try
                {
                    errorBody = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
                }
                catch { /* response body wasn't JSON, fall through to default message */ }

                ErrorMessage = response.StatusCode switch
                {
                    HttpStatusCode.GatewayTimeout or
                    HttpStatusCode.RequestTimeout =>
                        errorBody?.Message ?? "The search timed out. Try narrowing your filters.",

                    HttpStatusCode.ServiceUnavailable =>
                        errorBody?.Message ?? "The server is temporarily unavailable. Please try again.",

                    HttpStatusCode.Unauthorized =>
                        "Your session has expired. Please log in again.",

                    HttpStatusCode.NotFound =>
                        "No records found for the selected filters.",

                    _ => errorBody?.Message ?? "An unexpected error occurred. Please try again."
                };

                Beneficiaries = new List<BeneficiaryListItemDto>();
                TotalCount = 0;
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Cannot reach the server. Please check your connection or contact support.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "An unexpected error occurred. Please try again.";
            Console.Error.WriteLine(ex);
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }

        HasActiveFilter = true;
    }

    // Returns true if any filter besides the name fields / GeneralSearch / paging
    // is narrowing the search. Used to gate the fuzzy name fallback — see the
    // warning where it's called in LoadAsync().
    private bool HasNonNameFilters()
    {
        var f = Filter;
        return f.PsgcCodeRegion.HasValue
            || (f.PsgcCodeProvinces?.Any() ?? false)
            || (f.PsgcCodeMunicipalities?.Any() ?? false)
            || f.PsgcCodeBarangay.HasValue
            || (f.PaymentStatuses?.Any() ?? false)
            || (f.FilterPayrollQuarters?.Any() ?? false)
            || f.PaymentDateFrom.HasValue || f.PaymentDateTo.HasValue
            || f.DateAddedFrom.HasValue || f.DateAddedTo.HasValue
            || f.DateEndorsedFrom.HasValue || f.DateEndorsedTo.HasValue
            || f.IsCompliant.HasValue || !string.IsNullOrWhiteSpace(f.ComplianceMode)
            || f.IsEligible.HasValue || !string.IsNullOrWhiteSpace(f.EligibilityMode)
            || f.CoStatus.HasValue
            || f.ReplacementStatus.HasValue
            || (f.FindingStatus.HasValue && f.FindingStatus != 3)
            || f.Sex.HasValue
            || f.FilterModeOfPayment.HasValue
            || f.SpecificAge.HasValue
            || f.MilestoneYear.HasValue
            || f.SpecificBirthday.HasValue || f.BirthdayFrom.HasValue || f.BirthdayTo.HasValue
            || f.FilterQuarter.HasValue
            || f.FilterFiscalYear.HasValue
            || !string.IsNullOrWhiteSpace(f.FilterBatch)
            || f.FilterRefYear.HasValue
            || !string.IsNullOrWhiteSpace(f.FilterRegionRoman)
            || !string.IsNullOrWhiteSpace(f.Validator)
            || !string.IsNullOrWhiteSpace(f.BatchCode)
            || !string.IsNullOrWhiteSpace(f.DataQualityIssue);
    }

    // ✅ Internal — silently tries the fuzzy match and swaps it into the
    // current result set if anything is found. Never throws, never shows
    // its own error — if it fails, the user just sees a normal empty result,
    // same as before this feature existed.
    private async Task TryFuzzyFallbackAsync()
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/beneficiary/similar-names", Filter);
            if (!response.IsSuccessStatusCode) return;

            var result = await response.Content.ReadFromJsonAsync<PagedResultDto<BeneficiaryListItemDto>>();
            if (result != null && result.Items.Any())
            {
                Beneficiaries = result.Items;
                TotalCount = result.TotalCount;
                TotalPages = result.TotalPages;
                LastResultWasFuzzy = true;
            }
        }
        catch
        {
            // Silent — fuzzy fallback is a nice-to-have, not critical path.
            // TotalCount stays 0, user sees the normal "no records" message.
        }
    }
    public void NotifyStateChanged() => OnChange?.Invoke();

    // WHY: the bell badge needs the count; the grid's modal needs the full
    // Pairs list. Storing the full summary here means both read from the
    // SAME fetched data — one network call serves both.
    public PossibleDuplicateSummaryDto? GlobalDuplicateSummary { get; set; }

    public int DuplicatePairCount => GlobalDuplicateSummary?.TotalPairs ?? 0;

    // ✅ FIX: this is now a COMPUTED property derived directly from
    // GlobalDuplicateSummary, not a separately-tracked flag. Previously,
    // HasFetchedGlobalDuplicateSummary was set explicitly inside
    // EnsureGlobalDuplicateSummaryLoadedAsync — which meant if data was
    // populated through ANY other path (or if there was timing variance
    // around when that flag got set vs when GlobalDuplicateSummary itself
    // was assigned), the two could disagree: the badge would show a real
    // count while the dropdown still said "not scanned yet." Deriving it
    // directly from the data makes that mismatch structurally impossible —
    // there is only one source of truth now.
    public bool HasFetchedGlobalDuplicateSummary => GlobalDuplicateSummary is not null;

    public bool IsFetchingGlobalDuplicateSummary { get; private set; }
    public bool ShouldAutoOpenDuplicateModal { get; set; }

    // ✅ Call this when the user logs OUT, so the NEXT login starts fresh
    // rather than carrying over a stale result from a previous session that
    // might belong to a different user with different visible data.
    public void ResetGlobalDuplicateState()
    {
        GlobalDuplicateSummary = null;
        // HasFetchedGlobalDuplicateSummary no longer needs resetting —
        // it's computed, and goes back to false automatically once
        // GlobalDuplicateSummary is null.
    }

    public void RequestDuplicateModalOpen()
    {
        ShouldAutoOpenDuplicateModal = true;
        NotifyStateChanged();
    }

    public void ClearDuplicateModalOpenRequest()
    {
        ShouldAutoOpenDuplicateModal = false;
    }

    // WHY THIS IS SEPARATE: tracks "has a mutation happened that the
    // CURRENTLY LOADED grid page might not reflect" — independent of
    // duplicates entirely.
    public bool GridDataMayBeStale { get; set; }

    public void MarkGridDataStale()
    {
        GridDataMayBeStale = true;
        NotifyStateChanged();
    }
    // ✅ ADD THIS MISSING METHOD
    // In BeneficiaryStateService.cs - Replace the BuildDuplicateScanCacheKey method

    private string BuildDuplicateScanCacheKey(BeneficiaryFilterDto f)
    {
        static string N(object? v) => v?.ToString() ?? "null";

        // ✅ REAL FIX: List<T>.ToString() returns the type name —
        // "System.Collections.Generic.List`1[System.Int32]" — not the
        // contents. That means EVERY non-empty list collapsed to the
        // SAME string regardless of which IDs were inside it, so any
        // two different multi-select selections (e.g. Surigao del Sur
        // vs Agusan del Norte) produced an IDENTICAL cache key. The
        // client's _memoryCache.TryGetValue() then returned whichever
        // result was cached FIRST under that collapsed key, no matter
        // what you actually selected afterward. This is why text boxes
        // and single-selects worked fine (their .ToString() correctly
        // reflects their value) but every multi-select field (Provinces,
        // Municipalities, PaymentStatuses) was broken.
        static string ListN<T>(List<T>? list) => list != null && list.Any()
            ? string.Join(",", list.OrderBy(x => x))
            : "null";

        return string.Join("|",
            "dup_scan_v5",  // ✅ Version bump — invalidates every cache entry
                            // ever produced by the broken logic above, so old
                            // poisoned entries can't be coincidentally hit.
                            // ── Location ──────────────────────────────────────────────────
            N(f.PsgcCodeRegion),
            ListN(f.PsgcCodeProvinces),      // ✅ FIX: join actual contents
            ListN(f.PsgcCodeMunicipalities), // ✅ FIX: join actual contents
            N(f.PsgcCodeBarangay),
            // ── Name filters ─────────────────────────────────────────────
            N(f.LastName),
            N(f.FirstName),
            N(f.FullName),
            // ── Status ────────────────────────────────────────────────────
            ListN(f.PaymentStatuses),        // ✅ FIX: join actual contents
            ListN(f.FilterPayrollQuarters),  // ✅ new
            N(f.PaymentDate),
            N(f.PaymentDateFrom),
            N(f.PaymentDateTo),
            N(f.IsEligible),
            N(f.EligibilityMode),
            N(f.IsCompliant),
            N(f.ComplianceMode),
            N(f.CoStatus),
            N(f.ReplacementStatus),
            N(f.FindingStatus),
            N(f.Sex),
            N(f.FilterModeOfPayment),
            // ── Age / Birthday ────────────────────────────────────────────
            N(f.SpecificAge),
            N(f.MilestoneYear),
            N(f.SpecificBirthday),
            N(f.BirthdayFrom),
            N(f.BirthdayTo),
            // ── Reference number ──────────────────────────────────────────
            N(f.FilterQuarter),
            N(f.FilterBatch),
            N(f.FilterRefYear),
            N(f.FilterRegionRoman),
            // ── Date Added ────────────────────────────────────────────────
            N(f.DateAddedFrom),
            N(f.DateAddedTo),
            // ── Validator ──────────────────────────────────────────────────
            N(f.Validator),
            N(f.BatchCode),
            // ── General Search ────────────────────────────────────────────
            N(f.GeneralSearch),
            N(f.DataQualityIssue) // ✅ ADD
        );
    }
    public List<DuplicateScanNotificationDto> DuplicateScanNotifications { get; private set; } = new();
    public bool IsDuplicateScanInProgress { get; set; }
    private readonly Queue<BeneficiaryFilterDto> _pendingScanQueue = new();
    public event Action? OnDuplicateNotificationsChanged;
    private void NotifyDuplicateNotificationsChanged() => OnDuplicateNotificationsChanged?.Invoke();
    // Call this when a duplicate scan is triggered by a filter change
    public async Task QueueDuplicateScanforFilterAsync(BeneficiaryFilterDto filter)
    {
        var snapshot = CloneFilter(filter);
        // Create a notification entry immediately (loading state)
        var notification = new DuplicateScanNotificationDto
        {
            Id = Guid.NewGuid(),
            ScannedAt = DateTime.Now,
            Filter = snapshot,
            FilterDescription = BuildFilterDescription(snapshot),
            IsLoading = true
        };

        DuplicateScanNotifications.Insert(0, notification); //Newest first
        NotifyDuplicateNotificationsChanged();

        //Queue the scan
        _pendingScanQueue.Enqueue(snapshot);

        //If no scan is in progress, start processing the queue
        if (!IsDuplicateScanInProgress)
        {
            _ = ProcessDuplicateScanQueueAsync();

        }

    }
    private static BeneficiaryFilterDto CloneFilter(BeneficiaryFilterDto f) => new()
    {
        PsgcCodeRegion = f.PsgcCodeRegion,
        PsgcCodeProvinces = f.PsgcCodeProvinces?.ToList(),
        PsgcCodeMunicipalities = f.PsgcCodeMunicipalities?.ToList(),
        PsgcCodeBarangay = f.PsgcCodeBarangay,
        LastName = f.LastName,
        FirstName = f.FirstName,
        FullName = f.FullName,
        PaymentStatuses = f.PaymentStatuses?.ToList(),
        FilterPayrollQuarters = f.FilterPayrollQuarters?.ToList(), // ✅ new
        PaymentDate = f.PaymentDate,
        PaymentDateFrom = f.PaymentDateFrom,
        PaymentDateTo = f.PaymentDateTo,
        IsEligible = f.IsEligible,
        EligibilityMode = f.EligibilityMode,
        IsCompliant = f.IsCompliant,
        ComplianceMode = f.ComplianceMode,
        CoStatus = f.CoStatus,
        ReplacementStatus = f.ReplacementStatus,
        FindingStatus = f.FindingStatus,
        Sex = f.Sex,
        FilterModeOfPayment = f.FilterModeOfPayment,
        SpecificAge = f.SpecificAge,
        MilestoneYear = f.MilestoneYear,
        SpecificBirthday = f.SpecificBirthday,
        BirthdayFrom = f.BirthdayFrom,
        BirthdayTo = f.BirthdayTo,
        FilterQuarter = f.FilterQuarter,
        FilterBatch = f.FilterBatch,
        FilterRefYear = f.FilterRefYear,
        FilterRegionRoman = f.FilterRegionRoman,
        DateAddedFrom = f.DateAddedFrom,
        DateAddedTo = f.DateAddedTo,
        Validator = f.Validator,
        BatchCode = f.BatchCode,
        GeneralSearch = f.GeneralSearch,
        DataQualityIssue = f.DataQualityIssue,   // ✅ ADD
        PageNumber = f.PageNumber,
        PageSize = f.PageSize
    };

    private async Task ProcessDuplicateScanQueueAsync()
    {
        if (IsDuplicateScanInProgress) return;

        IsDuplicateScanInProgress = true;
        NotifyDuplicateNotificationsChanged();

        try
        {
            while (_pendingScanQueue.Count > 0)
            {
                var filter = _pendingScanQueue.Dequeue();

                // Find the corresponding notification
                var notification = DuplicateScanNotifications
                    .FirstOrDefault(n => n.IsLoading &&
                        AreFiltersEquivalent(n.Filter, filter));

                if (notification == null)
                {
                    notification = new DuplicateScanNotificationDto
                    {
                        Id = Guid.NewGuid(),
                        ScannedAt = DateTime.Now,
                        Filter = filter,
                        FilterDescription = BuildFilterDescription(filter),
                        IsLoading = true
                    };
                    DuplicateScanNotifications.Insert(0, notification);
                    NotifyDuplicateNotificationsChanged();
                }

                try
                {
                    var cacheKey = BuildDuplicateScanCacheKey(filter);

                    // Check cache first
                    if (_memoryCache.TryGetValue(cacheKey, out PossibleDuplicateSummaryDto? cached) && cached is not null)
                    {
                        notification.Result = cached;
                        notification.TotalPairs = cached.TotalPairs;
                        notification.IsLoading = false;
                        NotifyDuplicateNotificationsChanged();
                        continue;
                    }

                    // ✅ FIX: Use PostAsJsonAsync with cancellation token
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                    var response = await _http.PostAsJsonAsync("api/beneficiary/possible-duplicates", filter, cts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<PossibleDuplicateSummaryDto>();
                        if (result != null)
                        {
                            notification.Result = result;
                            notification.TotalPairs = result.TotalPairs;
                            notification.IsLoading = false;

                            _memoryCache.Set(cacheKey, result, new MemoryCacheEntryOptions
                            {
                                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                                SlidingExpiration = TimeSpan.FromMinutes(3)
                            });
                        }
                    }
                    else
                    {
                        notification.HasError = true;
                        notification.ErrorMessage = $"Server error: {response.StatusCode}";
                        notification.IsLoading = false;
                    }
                }
                catch (OperationCanceledException)
                {
                    notification.HasError = true;
                    notification.ErrorMessage = "The duplicate scan timed out. Please try again.";
                    notification.IsLoading = false;
                }
                catch (Exception ex)
                {
                    notification.IsLoading = false;
                    notification.HasError = true;
                    notification.ErrorMessage = ex.Message;
                }

                NotifyDuplicateNotificationsChanged();
            }
        }
        finally
        {
            IsDuplicateScanInProgress = false;
            NotifyDuplicateNotificationsChanged();
        }
    }
    // In BeneficiaryStateService.cs - Replace the AreFiltersEquivalent method

    private bool AreFiltersEquivalent(BeneficiaryFilterDto a, BeneficiaryFilterDto b)
    {
        // Compare ALL filter properties that affect the scan
        return
            // Location
            a.PsgcCodeRegion == b.PsgcCodeRegion &&
            AreListsEqual(a.PsgcCodeProvinces, b.PsgcCodeProvinces) &&
            AreListsEqual(a.PsgcCodeMunicipalities, b.PsgcCodeMunicipalities) &&
            a.PsgcCodeBarangay == b.PsgcCodeBarangay &&

            // Name filters
            a.LastName == b.LastName &&
            a.FirstName == b.FirstName &&
            a.FullName == b.FullName &&

            // Status
            AreListsEqual(a.PaymentStatuses, b.PaymentStatuses) &&
            AreListsEqual(a.FilterPayrollQuarters, b.FilterPayrollQuarters) && // ✅ new
            a.PaymentDate == b.PaymentDate &&
            a.PaymentDateFrom == b.PaymentDateFrom &&
            a.PaymentDateTo == b.PaymentDateTo &&
            a.IsEligible == b.IsEligible &&
            a.EligibilityMode == b.EligibilityMode &&
            a.IsCompliant == b.IsCompliant &&
            a.ComplianceMode == b.ComplianceMode &&
            a.CoStatus == b.CoStatus &&
            a.ReplacementStatus == b.ReplacementStatus &&
            a.FindingStatus == b.FindingStatus &&
            a.Sex == b.Sex &&
            a.FilterModeOfPayment == b.FilterModeOfPayment &&

            // Age / Birthday
            a.SpecificAge == b.SpecificAge &&
            a.MilestoneYear == b.MilestoneYear &&
            a.SpecificBirthday == b.SpecificBirthday &&
            a.BirthdayFrom == b.BirthdayFrom &&
            a.BirthdayTo == b.BirthdayTo &&

            // Reference number
            a.FilterQuarter == b.FilterQuarter &&
            a.FilterBatch == b.FilterBatch &&
            a.FilterRefYear == b.FilterRefYear &&
            a.FilterRegionRoman == b.FilterRegionRoman &&

            // Date Added
            a.DateAddedFrom == b.DateAddedFrom &&
            a.DateAddedTo == b.DateAddedTo &&

            // Other
            a.Validator == b.Validator &&
            a.BatchCode == b.BatchCode &&
            a.GeneralSearch == b.GeneralSearch;
    }

    // Helper method to compare lists
    private bool AreListsEqual<T>(List<T>? a, List<T>? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        return a.OrderBy(x => x).SequenceEqual(b.OrderBy(x => x));
    }
    // In BeneficiaryStateService.cs - Update BuildFilterDescription

    private string BuildFilterDescription(BeneficiaryFilterDto filter)
    {
        var parts = new List<string>();

        // ── Location ──────────────────────────────────────────────────────────────
        if (filter.PsgcCodeRegion.HasValue)
        {
            var regionName = GetRegionName(filter.PsgcCodeRegion.Value);
            parts.Add($"Region: {regionName}");
        }
        if (filter.PsgcCodeProvinces != null && filter.PsgcCodeProvinces.Any())
        {
            var names = filter.PsgcCodeProvinces
                .Select(p => GetProvinceName(p))
                .Where(n => !string.IsNullOrEmpty(n));
            parts.Add($"Provinces: {string.Join(", ", names)}");
        }
        if (filter.PsgcCodeMunicipalities != null && filter.PsgcCodeMunicipalities.Any())
        {
            var names = filter.PsgcCodeMunicipalities
                .Select(m => GetMunicipalityName(m))
                .Where(n => !string.IsNullOrEmpty(n));
            parts.Add($"Municipalities: {string.Join(", ", names)}");
        }
        if (filter.PsgcCodeBarangay.HasValue)
        {
            var barangayName = GetBarangayName(filter.PsgcCodeBarangay.Value);
            parts.Add($"Barangay: {barangayName}");
        }

        // ── Name filters ──────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(filter.FullName))
            parts.Add($"Name: {filter.FullName}");
        else
        {
            if (!string.IsNullOrWhiteSpace(filter.LastName))
                parts.Add($"Last: {filter.LastName}");
            if (!string.IsNullOrWhiteSpace(filter.FirstName))
                parts.Add($"First: {filter.FirstName}");
        }

        // ── Status filters ───────────────────────────────────────────────────────
        if (filter.PaymentStatuses != null && filter.PaymentStatuses.Any())
        {
            var labels = filter.PaymentStatuses.Select(GetPaymentStatusLabel);
            parts.Add($"Payment: {string.Join(", ", labels)}");
        }
        if (filter.FilterPayrollQuarters != null && filter.FilterPayrollQuarters.Any()) // ✅ new
        {
            parts.Add($"Payroll Quarter: {string.Join(", ", filter.FilterPayrollQuarters.Select(q => $"Q{q}"))}");
        }
        if (filter.IsEligible.HasValue)
            parts.Add($"Eligible: {(filter.IsEligible.Value ? "Yes" : "No")}");
        if (!string.IsNullOrWhiteSpace(filter.EligibilityMode))
            parts.Add($"Eligibility: {filter.EligibilityMode}");
        if (filter.IsCompliant.HasValue)
            parts.Add($"Compliant: {(filter.IsCompliant.Value ? "Yes" : "No")}");
        if (!string.IsNullOrWhiteSpace(filter.ComplianceMode))
            parts.Add($"Compliance: {filter.ComplianceMode}");
        if (filter.Sex.HasValue)
            parts.Add($"Sex: {(filter.Sex.Value == 1 ? "Male" : "Female")}");
        if (filter.CoStatus.HasValue)
            parts.Add($"CO Status: {GetCoStatusLabel(filter.CoStatus.Value)}");
        if (filter.ReplacementStatus.HasValue)
            parts.Add($"Replacement Status: {GetReplacementStatusLabel(filter.ReplacementStatus.Value)}");
        if (filter.FindingStatus.HasValue && filter.FindingStatus != 3)
            parts.Add($"Findings: {GetFindingStatusLabel(filter.FindingStatus.Value)}");
        if (filter.FilterModeOfPayment.HasValue)
            parts.Add($"Mode of Payment: {GetModeOfPaymentLabel(filter.FilterModeOfPayment.Value)}");

        // ── Age & Birthday ──────────────────────────────────────────────────────
        if (filter.SpecificAge.HasValue)
            parts.Add($"Age: {filter.SpecificAge}");
        if (filter.MilestoneYear.HasValue)
            parts.Add($"Milestone: {filter.MilestoneYear}");
        if (filter.SpecificBirthday.HasValue)
            parts.Add($"Birthday: {filter.SpecificBirthday:MMM dd}");
        if (filter.BirthdayFrom.HasValue || filter.BirthdayTo.HasValue)
        {
            var from = filter.BirthdayFrom?.ToString("MMM dd") ?? "any";
            var to = filter.BirthdayTo?.ToString("MMM dd") ?? "any";
            parts.Add($"Birthday Range: {from} - {to}");
        }

        // ── Reference number ─────────────────────────────────────────────────────
        if (filter.FilterQuarter.HasValue)
            parts.Add($"Quarter: Q{filter.FilterQuarter}");
        if (!string.IsNullOrWhiteSpace(filter.FilterBatch))
            parts.Add($"Batch: {filter.FilterBatch}");
        if (filter.FilterRefYear.HasValue)
            parts.Add($"Year: {filter.FilterRefYear}");
        if (!string.IsNullOrWhiteSpace(filter.FilterRegionRoman))
            parts.Add($"Region: {filter.FilterRegionRoman}");

        // ── Date ranges ─────────────────────────────────────────────────────────
        if (filter.PaymentDateFrom.HasValue || filter.PaymentDateTo.HasValue)
        {
            var from = filter.PaymentDateFrom?.ToShortDateString() ?? "any";
            var to = filter.PaymentDateTo?.ToShortDateString() ?? "any";
            parts.Add($"Payment Date: {from} - {to}");
        }
        if (filter.DateAddedFrom.HasValue || filter.DateAddedTo.HasValue)
        {
            var from = filter.DateAddedFrom?.ToShortDateString() ?? "any";
            var to = filter.DateAddedTo?.ToShortDateString() ?? "any";
            parts.Add($"Date Added: {from} - {to}");
        }

        // ── Other ────────────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(filter.Validator))
            parts.Add($"Validator: {filter.Validator}");
        if (!string.IsNullOrWhiteSpace(filter.BatchCode))
            parts.Add($"Batch Code: {filter.BatchCode}");
        if (!string.IsNullOrWhiteSpace(filter.GeneralSearch))
            parts.Add($"Search: {filter.GeneralSearch}");
        if (!string.IsNullOrWhiteSpace(filter.DataQualityIssue))
        {
            var label = filter.DataQualityIssue switch
            {
                "location" => "Caution — Location Needs Correction",
                "headsup" => "Heads-Up — Missing Payment Info",
                "incomplete" => "Incomplete — Missing Grantee Details",
                _ => filter.DataQualityIssue
            };
            parts.Add($"Data Quality: {label}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "Full Dataset";
    }

    // Helper methods for labels
    private string GetFindingStatusLabel(int status) => status switch
    {
        0 => "N/A",
        1 => "Solved",
        2 => "Unresolved",
        _ => status.ToString()
    };

    private string GetModeOfPaymentLabel(int mode) => mode switch
    {
        1 => "Cash Advance",
        2 => "Bank Transfer",
        _ => mode.ToString()
    };
    // Helper methods to get names from cache
    private string GetRegionName(int regionCode)
    {
        var region = FilterRegions.FirstOrDefault(r => r.PsgcCodeRegion == regionCode);
        return region?.Name ?? regionCode.ToString();
    }

    private string GetProvinceName(int provinceCode)
    {
        var province = FilterProvinces.FirstOrDefault(p => p.PsgcCodeProvince == provinceCode);
        return province?.Name ?? provinceCode.ToString();
    }

    private string GetMunicipalityName(int municipalityCode)
    {
        var municipality = FilterMunicipalities.FirstOrDefault(m => m.PsgcCodeMunicipality == municipalityCode);
        return municipality?.Name ?? municipalityCode.ToString();
    }

    private string GetBarangayName(int barangayCode)
    {
        var barangay = FilterBarangays.FirstOrDefault(b => b.PsgcCodeBarangay == barangayCode);
        return barangay?.Name ?? barangayCode.ToString();
    }

    private string GetPaymentStatusLabel(int status) => status switch
    {
        0 => "N/A",
        1 => "Unpaid",
        2 => "Paid",
        3 => "Pending",
        _ => status.ToString()
    };

    private string GetCoStatusLabel(int status) => status switch
    {
        0 => "Not Set",
        1 => "Endorsed",
        2 => "Approved",
        _ => status.ToString()
    };

    private string GetReplacementStatusLabel(int status) => status switch
    {
        0 => "Not Replaced",
        1 => "Replaced",
        2 => "Is Replacement",
        _ => status.ToString()
    };

    public async Task<bool> ResolveDuplicatePairAsync(PossibleDuplicatePairDto pair, string? remarks)
    {
        try
        {
            var request = new ResolveDuplicatePairRequestDto
            {
                Record1Id = pair.Record1Id,
                Record2Id = pair.Record2Id,
                Remarks = remarks
            };

            var response = await _http.PostAsJsonAsync("api/beneficiary/possible-duplicates/resolve", request);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<PossibleDuplicatePairDto>();
            if (result is null) return false;

            ApplyResolutionToMatchingPairs(pair, result);
            NotifyDuplicateNotificationsChanged();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UnresolveDuplicatePairAsync(PossibleDuplicatePairDto pair)
    {
        try
        {
            var request = new UnresolveDuplicatePairRequestDto
            {
                Record1Id = pair.Record1Id,
                Record2Id = pair.Record2Id
            };

            var response = await _http.PostAsJsonAsync("api/beneficiary/possible-duplicates/unresolve", request);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<PossibleDuplicatePairDto>();
            if (result is null) return false;

            ApplyResolutionToMatchingPairs(pair, result);
            NotifyDuplicateNotificationsChanged();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // The same beneficiary pair can be cached inside more than one notification's
    // result (overlapping filters can both surface it) — update every occurrence
    // so the bell stays consistent no matter which notification the user acted from.
    private void ApplyResolutionToMatchingPairs(PossibleDuplicatePairDto pair, PossibleDuplicatePairDto result)
    {
        foreach (var notification in DuplicateScanNotifications)
        {
            var match = notification.Result?.Pairs.FirstOrDefault(p =>
                (p.Record1Id == pair.Record1Id && p.Record2Id == pair.Record2Id) ||
                (p.Record1Id == pair.Record2Id && p.Record2Id == pair.Record1Id));

            if (match is null) continue;

            match.IsResolved = result.IsResolved;
            match.Remarks = result.Remarks;
            match.ResolvedAt = result.ResolvedAt;
            match.ResolvedBy = result.ResolvedBy;
        }
    }

    public void ClearDuplicateNotification(Guid notificationId)
    {
        var notification = DuplicateScanNotifications.FirstOrDefault(n => n.Id == notificationId);
        if (notification != null)
        {
            DuplicateScanNotifications.Remove(notification);
            NotifyDuplicateNotificationsChanged();
        }
    }

    public void ClearAllDuplicateNotifications()
    {
        DuplicateScanNotifications.Clear();
        NotifyDuplicateNotificationsChanged();
    }

    public void ClearOldDuplicateNotifications(int keepCount = 50)
    {
        if (DuplicateScanNotifications.Count > keepCount)
        {
            // Keep the newest ones
            var toRemove = DuplicateScanNotifications.Skip(keepCount).ToList();
            foreach (var item in toRemove)
            {
                DuplicateScanNotifications.Remove(item);
            }
            NotifyDuplicateNotificationsChanged();
        }
    }

    // Keep the existing methods but redirect them to use the new queued system
    // For backward compatibility
    public async Task EnsureGlobalDuplicateSummaryLoadedAsync()
    {
        if (HasFetchedGlobalDuplicateSummary || IsFetchingGlobalDuplicateSummary)
            return;

        await RefreshGlobalDuplicateSummaryAsync();
    }

    public async Task RefreshGlobalDuplicateSummaryAsync()
    {
        // This is now a special case - the "global" scan (all records)
        // We'll treat this as a filter with no filters applied
        var emptyFilter = new BeneficiaryFilterDto
        {
            PageNumber = 1,
            PageSize = 10
        };

        await QueueDuplicateScanforFilterAsync(emptyFilter);
    }
    // Total pairs including loading ones
    public int TotalPendingScans => DuplicateScanNotifications.Count(n => n.IsLoading);

    public bool HasPendingDuplicates => DuplicateScanNotifications.Any(n => !n.IsLoading && n.TotalPairs > 0);
}