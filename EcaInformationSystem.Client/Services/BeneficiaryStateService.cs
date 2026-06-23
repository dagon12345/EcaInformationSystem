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

    // WHY: currently the service only stores DuplicatePairCount (an int) —
    // the bell badge needs nothing more than that. But the GRID'S MODAL needs
    // the full Pairs list to render the table, and was making its own
    // redundant fetch to get it. Storing the full summary here means BOTH
    // the bell and the modal read from the SAME already-fetched data —
    // one network call serves both, instead of two separate calls doing
    // almost the same work.
    public PossibleDuplicateSummaryDto? GlobalDuplicateSummary { get; private set; }
    public int DuplicatePairCount => GlobalDuplicateSummary?.TotalPairs ?? 0;
    // ✅ DuplicatePairCount is now a computed property derived from the full
    // summary, not a separately-tracked field — impossible for the two to
    // drift out of sync with each other.
    public bool HasFetchedGlobalDuplicateSummary { get; set; }
    public bool IsFetchingGlobalDuplicateSummary { get; set; }
    public bool ShouldAutoOpenDuplicateModal { get; set; }

    public async Task EnsureGlobalDuplicateSummaryLoadedAsync(HttpClient http)
    {
        // ✅ The actual "only once" guard — once this flips true, no caller,
        // from any page, at any time, will trigger another fetch. The ONLY
        // way the count updates after this is via RefreshGlobalDuplicateSummaryAsync,
        // called explicitly after a data-changing action.
        if (HasFetchedGlobalDuplicateSummary || IsFetchingGlobalDuplicateSummary)
            return;

        await RefreshGlobalDuplicateSummaryAsync(http);
        HasFetchedGlobalDuplicateSummary = true;
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
    // rather than carrying over a stale flag from a previous session that
    // might belong to a different user with different visible data.
    public void ResetGlobalDuplicateState()
    {
        HasFetchedGlobalDuplicateSummary = false;
        GlobalDuplicateSummary = null;
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
    // WHY THIS IS SEPARATE: this tracks "has a mutation happened that the
    // CURRENTLY LOADED grid page might not reflect" — independent of
    // duplicates entirely. A bulk payment update, a CO status change, a
    // soft delete — any of these could leave the on-screen grid stale even
    // though the user hasn't navigated away. This flag is the signal for that.
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
