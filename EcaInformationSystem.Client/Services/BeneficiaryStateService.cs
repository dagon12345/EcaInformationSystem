using EcaInformationSystem.Shared.DTOs;
using System.Net.Http.Json;

public class BeneficiaryStateService
{
    private readonly HttpClient _http;
    public BeneficiaryStateService(HttpClient http) => _http = http;

    // Shared Data
    public BeneficiaryFilterDto Filter { get; set; } = new()
    {
        PageNumber = 1,
        PageSize = 10
    };
    // ✅ Add this method
    public void ClearData()
    {
        Beneficiaries = new List<BeneficiaryInformationDto>();
        TotalCount = 0;
        TotalPages = 0;
        NotifyStateChanged();
    }
    public List<BeneficiaryInformationDto> Beneficiaries { get; private set; } = new();
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public bool IsLoading { get; private set; }

    // ✅ Add these two
    public DateTime? LastLoaded { get; private set; }

    public void Invalidate() => LastLoaded = null; // ✅ forces reload on next visit

    public bool IsStale() =>
        LastLoaded == null ||
        (DateTime.Now - LastLoaded.Value).TotalSeconds > 30; // ✅ also catches tab switches


    // Event to notify components of changes
    public event Action? OnChange;

    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            NotifyStateChanged();

            // Use the dynamic query string helper
            var query = GetQueryString(Filter);
            var result = await _http.GetFromJsonAsync<PagedResultDto<BeneficiaryInformationDto>>($"api/beneficiary/paged?{query}");

            Beneficiaries = result?.Items?.ToList() ?? new();
            TotalCount = result?.TotalCount ?? 0;
            TotalPages = result?.TotalPages ?? 0; 
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    private string GetQueryString(BeneficiaryFilterDto filter)
    {
        var queryString = new List<string>();
        foreach (var prop in filter.GetType().GetProperties())
        {
            var value = prop.GetValue(filter, null);
            if (value == null) continue;

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
