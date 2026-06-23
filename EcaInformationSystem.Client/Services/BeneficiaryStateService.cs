using EcaInformationSystem.Shared.DTOs;
using System.Net;
using System.Net.Http.Json;

public class BeneficiaryStateService
{
    private readonly HttpClient _http;
    public BeneficiaryStateService(HttpClient http) => _http = http;

    public bool HasActiveFilter { get; private set; } = false;

    // ✅ Persist filter UI selections across navigation
    public int SelectedRegionId { get; set; } = 0;
    public int SelectedProvinceId { get; set; } = 0;
    public int SelectedMunicipalityId { get; set; } = 0;
    public int SelectedBarangayId { get; set; } = 0;
    public int SelectedSex { get; set; }
    public int SelectedPaymentStatus { get; set; }
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
        ErrorMessage = null; // ← clear previous error
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
            }
            else
            {
                // Read the structured error from your global exception handler
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

    private string GetQueryString(BeneficiaryFilterDto filter)
    {
        var queryString = new List<string>();
        foreach (var prop in filter.GetType().GetProperties())
        {
            var value = prop.GetValue(filter, null);
            if (value == null) continue;

            // ✅ Skip sentinel values — don't send -1 or 0 for Sex/PaymentStatus
            if (value is int intVal && intVal < 0) continue;

            // ✅ Skip FindingStatus sentinel (3 = "not selected")
            if (prop.Name == nameof(BeneficiaryFilterDto.FindingStatus) && value is int fs && fs == 3) continue;

            if (value is System.Collections.IEnumerable list && !(value is string))
            {
                foreach (var item in list)
                    queryString.Add($"{prop.Name}={Uri.EscapeDataString(item.ToString() ?? "")}");
            }
            else
            {
                queryString.Add($"{prop.Name}={Uri.EscapeDataString(value.ToString() ?? "")}");
            }
        }
        return string.Join("&", queryString);
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    // WHY: the bell badge needs the count; the grid's modal needs the full
    // Pairs list. Storing the full summary here means both read from the
    // SAME fetched data — one network call serves both.
    public PossibleDuplicateSummaryDto? GlobalDuplicateSummary { get; private set; }

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

    // ✅ Kept for backward compatibility with any caller that still invokes
    // this — but since HasFetchedGlobalDuplicateSummary is now computed,
    // this just becomes "fetch once unless already fetched," same as before,
    // without needing to manually flip a flag afterward.
    public async Task EnsureGlobalDuplicateSummaryLoadedAsync(HttpClient http)
    {
        if (HasFetchedGlobalDuplicateSummary || IsFetchingGlobalDuplicateSummary)
            return;

        await RefreshGlobalDuplicateSummaryAsync(http);
    }

    public async Task RefreshGlobalDuplicateSummaryAsync(HttpClient http)
    {
        try
        {
            IsFetchingGlobalDuplicateSummary = true;
            NotifyStateChanged();

            GlobalDuplicateSummary = await http.GetFromJsonAsync<PossibleDuplicateSummaryDto>(
                "api/beneficiary/global-duplicate-summary");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Global duplicate summary fetch failed: {ex.Message}");
        }
        finally
        {
            IsFetchingGlobalDuplicateSummary = false;
            NotifyStateChanged();
        }
    }

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

    public void ClearGridDataStaleFlag()
    {
        GridDataMayBeStale = false;
    }
}