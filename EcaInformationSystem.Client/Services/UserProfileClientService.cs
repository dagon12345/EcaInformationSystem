using EcaInformationSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    // Scoped so it's shared per-circuit — PostFeed's profile card and
    // ProfileContent's own page both read from the same cached fetch instead
    // of each hitting api/UserProfile/me separately on every load (that
    // endpoint alone does 4 DB round trips server-side per call). Mirrors
    // TransactionTierClientService's in-memory-cache + in-flight-coalescing
    // pattern.
    public class UserProfileClientService
    {
        private readonly HttpClient _http;
        private UserProfileDto? _cachedMine;
        private Task<UserProfileDto?>? _inFlightMine;

        public event Action? OnChange;

        public UserProfileClientService(HttpClient http)
        {
            _http = http;
        }

        public async Task<UserProfileDto?> GetMyProfileAsync(bool forceRefresh = false)
        {
            if (_cachedMine is not null && !forceRefresh)
                return _cachedMine;

            // Coalesce concurrent callers (PostFeed + ProfileContent can both
            // ask at once on first page load) into a single HTTP request.
            _inFlightMine ??= FetchMineAsync();
            var result = await _inFlightMine;
            _inFlightMine = null;
            return result;
        }

        private async Task<UserProfileDto?> FetchMineAsync()
        {
            try
            {
                _cachedMine = await _http.GetFromJsonAsync<UserProfileDto>("api/UserProfile/me");
                OnChange?.Invoke();
                return _cachedMine;
            }
            catch
            {
                return null;
            }
        }

        // Called right after a successful PUT api/UserProfile/me — pushes the
        // server's fresh response straight into the cache instead of
        // invalidating and forcing every reader to refetch over the network.
        public void SetCachedProfile(UserProfileDto profile)
        {
            _cachedMine = profile;
            OnChange?.Invoke();
        }

        // Called when something changed the profile server-side but we don't
        // have the fresh object on hand (rare) — next read re-fetches.
        public void Invalidate()
        {
            _cachedMine = null;
            OnChange?.Invoke();
        }
    }
}
