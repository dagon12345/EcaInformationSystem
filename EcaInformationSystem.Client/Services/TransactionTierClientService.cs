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
    }
}
