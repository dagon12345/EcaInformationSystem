using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    /// <summary>
    /// Independent state holder for the Liquidation (CDR) page.
    /// Deliberately NOT shared with BeneficiaryStateService (Records page) —
    /// they were previously tied together via the same injected service,
    /// which meant applying a filter on one page silently mutated what the
    /// other page displayed. This class owns its own Filter, Beneficiaries,
    /// and cache, completely isolated from Records.
    /// </summary>
    public class LiquidationStateService
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _memoryCache;

        // ✅ Cache key prefix — guarantees this never collides with any cache
        // key produced by BeneficiaryStateService's duplicate-scan cache or
        // any other IMemoryCache consumer sharing the same cache instance.
        private const string CachePrefix = "liquidation_paged_v1";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public LiquidationStateService(HttpClient http, IMemoryCache memoryCache)
        {
            _http = http;
            _memoryCache = memoryCache;
        }

        // ── Filter UI selections — persisted across navigation within Liquidation ──
        public int SelectedRegionId { get; set; } = 0;
        public int SelectedProvinceId { get; set; } = 0;
        public DateTime? PaymentDateFrom { get; set; }
        public DateTime? PaymentDateTo { get; set; }

        // ── Persisted dropdown lookups (avoid reload on back-navigation) ──────────
        public List<RegionLookupDto> FilterRegions { get; set; } = new();
        public List<ProvinceLookupDto> FilterProvinces { get; set; } = new();

        // ── Core state ──────────────────────────────────────────────────────────
        public BeneficiaryFilterDto Filter { get; set; } = new()
        {
            PageNumber = 1,
            PageSize = 10000,
            PaymentStatus = 2 // Liquidation always deals with Paid records only
        };

        public List<BeneficiaryListItemDto> Beneficiaries { get; private set; } = new();
        public int TotalCount { get; private set; }
        public bool IsLoading { get; private set; }
        public bool HasActiveFilter { get; private set; }
        public string? ErrorMessage { get; set; }
        public DateTime? LastLoaded { get; private set; }

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public bool IsStale() =>
            LastLoaded == null ||
            (DateTime.Now - LastLoaded.Value).TotalSeconds > 30;

        // ── Load (with client-side caching) ────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            ErrorMessage = null;
            NotifyStateChanged();

            try
            {
                var cacheKey = BuildCacheKey(Filter);

                if (_memoryCache.TryGetValue(cacheKey, out PagedResultDto<BeneficiaryListItemDto>? cached)
                    && cached is not null)
                {
                    Beneficiaries = cached.Items;
                    TotalCount = cached.TotalCount;
                }
                else
                {
                    var response = await _http.PostAsJsonAsync("api/beneficiary/paged-list", Filter);

                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content
                            .ReadFromJsonAsync<PagedResultDto<BeneficiaryListItemDto>>();

                        Beneficiaries = result?.Items ?? new List<BeneficiaryListItemDto>();
                        TotalCount = result?.TotalCount ?? 0;

                        if (result != null)
                        {
                            _memoryCache.Set(cacheKey, result, new MemoryCacheEntryOptions
                            {
                                AbsoluteExpirationRelativeToNow = CacheDuration,
                                SlidingExpiration = TimeSpan.FromMinutes(2)
                            });
                        }
                    }
                    else
                    {
                        ApiErrorResponse? errorBody = null;
                        try
                        {
                            errorBody = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
                        }
                        catch { /* body wasn't JSON */ }

                        ErrorMessage = response.StatusCode switch
                        {
                            HttpStatusCode.GatewayTimeout or HttpStatusCode.RequestTimeout =>
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
                LastLoaded = DateTime.Now;
                NotifyStateChanged();
            }

            HasActiveFilter = true;
        }

        public void ClearData()
        {
            Beneficiaries = new List<BeneficiaryListItemDto>();
            TotalCount = 0;
            LastLoaded = null;
            HasActiveFilter = false;
            ErrorMessage = null;
            NotifyStateChanged();
        }

        // ✅ Explicit invalidation — call this if a beneficiary's PaymentStatus,
        // PaymentDate, or Province is edited elsewhere in the app, so the next
        // Liquidation load doesn't serve stale cached results.
        public void InvalidateCache(BeneficiaryFilterDto? forFilter = null)
        {
            if (forFilter != null)
            {
                _memoryCache.Remove(BuildCacheKey(forFilter));
            }
            LastLoaded = null;
        }

        // ── Cache key builder ──────────────────────────────────────────────────
        // Only includes the fields Liquidation's filter actually sets
        // (Region, Province, PaymentDateFrom/To, PaymentStatus, paging).
        // Keeping this narrow means the key stays accurate to what Liquidation
        // can even select, and won't silently collide with a Records-page key
        // even if they somehow shared the same IMemoryCache instance.
        private static string BuildCacheKey(BeneficiaryFilterDto f)
        {
            static string N(object? v) => v?.ToString() ?? "null";

            return string.Join("|",
                CachePrefix,
                N(f.PsgcCodeRegion),
                N(f.PsgcCodeProvince),
                N(f.PaymentStatus),
                N(f.PaymentDateFrom),
                N(f.PaymentDateTo),
                N(f.PageNumber),
                N(f.PageSize)
            );
        }
    }
}