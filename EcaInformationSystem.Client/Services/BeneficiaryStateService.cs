using EcaInformationSystem.Shared.DTOs;
using System.Net;
using System.Net.Http.Json;

public class BeneficiaryStateService
{
    private readonly HttpClient _http;
    public BeneficiaryStateService(HttpClient http) => _http = http;

    public bool HasActiveFilter { get; private set; } = false;

    // ✅ Persist filter UI selections across navigation
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

    public List<BeneficiaryInformationDto> Beneficiaries { get; private set; } = new();
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public bool IsLoading { get; private set; }


    // ✅ Add these two
    public DateTime? LastLoaded { get; private set; }
    public void SetBeneficiaries(List<BeneficiaryInformationDto> items)
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
        Beneficiaries = new List<BeneficiaryInformationDto>();
        TotalCount = 0;
        TotalPages = 0;
        LastLoaded = null;
        HasActiveFilter = false; // ✅ reset on clear
        NotifyStateChanged();
    }

    public async Task LoadAsync()
    {
        ErrorMessage = null; // ← clear previous error

        try
        {
            IsLoading = true;
            NotifyStateChanged();

            var query = GetQueryString(Filter);
            var response = await _http.GetAsync($"api/beneficiary/paged?{query}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PagedResultDto<BeneficiaryInformationDto>>();
                Beneficiaries = result?.Items?.ToList() ?? new();
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
}
