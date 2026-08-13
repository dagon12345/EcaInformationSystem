using EcaInformationSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    // Scoped so it's shared per-circuit — NavMenu's badge, Profile's tier
    // card, and PostFeed's leaderboard all read from the same cached fetch
    // instead of double-hitting the API on every page load.
    public class TransactionTierClientService
    {
        private readonly HttpClient _http;
        private UserTransactionTierDto? _cachedMine;
        private Task<UserTransactionTierDto?>? _inFlightMine;
        private List<UserLeaderboardEntryDto>? _cachedLeaderboard;
        private Task<List<UserLeaderboardEntryDto>>? _inFlightLeaderboard;
        private LeaderboardSeasonInfoDto? _cachedSeason;

        public event Action? OnChange;

        public TransactionTierClientService(HttpClient http)
        {
            _http = http;
        }

        public async Task<UserTransactionTierDto?> GetMyTierAsync(bool forceRefresh = false)
        {
            if (_cachedMine is not null && !forceRefresh)
                return _cachedMine;

            // Coalesce concurrent callers (NavMenu + Profile can both ask at
            // once on first page load) into a single HTTP request.
            _inFlightMine ??= FetchMineAsync();
            var result = await _inFlightMine;
            _inFlightMine = null;
            return result;
        }

        private async Task<UserTransactionTierDto?> FetchMineAsync()
        {
            try
            {
                _cachedMine = await _http.GetFromJsonAsync<UserTransactionTierDto>("api/transaction-tier/me");
                OnChange?.Invoke();
                return _cachedMine;
            }
            catch
            {
                return null;
            }
        }

        // Not cached — used only when viewing someone else's profile, which
        // isn't a hot path like "me"/leaderboard, so no coalescing needed.
        public async Task<UserTransactionTierDto?> GetTierForUserAsync(Guid userId)
        {
            try
            {
                return await _http.GetFromJsonAsync<UserTransactionTierDto>($"api/transaction-tier/user/{userId}");
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<UserLeaderboardEntryDto>> GetLeaderboardAsync(bool forceRefresh = false)
        {
            if (_cachedLeaderboard is not null && !forceRefresh)
                return _cachedLeaderboard;

            _inFlightLeaderboard ??= FetchLeaderboardAsync();
            var result = await _inFlightLeaderboard;
            _inFlightLeaderboard = null;
            return result;
        }

        private async Task<List<UserLeaderboardEntryDto>> FetchLeaderboardAsync()
        {
            try
            {
                _cachedLeaderboard = await _http.GetFromJsonAsync<List<UserLeaderboardEntryDto>>("api/transaction-tier/leaderboard") ?? new();
                return _cachedLeaderboard;
            }
            catch
            {
                return new();
            }
        }

        public async Task<LeaderboardSeasonInfoDto?> GetSeasonInfoAsync(bool forceRefresh = false)
        {
            if (_cachedSeason is not null && !forceRefresh)
                return _cachedSeason;

            try
            {
                _cachedSeason = await _http.GetFromJsonAsync<LeaderboardSeasonInfoDto>("api/transaction-tier/season");
                return _cachedSeason;
            }
            catch
            {
                return null;
            }
        }

        // SuperAdmin only (enforced server-side too) — ends the active season
        // now instead of waiting for the automatic Sunday 11:59 PM reset.
        public async Task<(bool Success, LeaderboardResetResultDto? Result, string? Error)> ResetSeasonNowAsync()
        {
            try
            {
                var response = await _http.PostAsync("api/transaction-tier/reset", null);
                if (!response.IsSuccessStatusCode)
                    return (false, null, await response.Content.ReadAsStringAsync());

                var result = await response.Content.ReadFromJsonAsync<LeaderboardResetResultDto>();
                InvalidateAfterReset();
                return (true, result, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        // Called on our own manual reset AND when the hub push tells us someone
        // else (or the automatic job) reset it — next read re-fetches fresh data.
        public void InvalidateAfterReset()
        {
            _cachedMine = null;
            _cachedLeaderboard = null;
            _cachedSeason = null;
            OnChange?.Invoke();
        }
    }
}
