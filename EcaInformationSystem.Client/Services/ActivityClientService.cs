using EcaInformationSystem.Shared.DTOs.Activity;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class ActivityClientService : IAsyncDisposable
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;
        private HubConnection? _hubConnection;

        public event Action<ActivityDto>? ActivityStarting;
        public event Action<ActivityDto>? ActivityChanged;
        public event Action<int>? ActivityDeleted;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public ActivityClientService(HttpClient http, IJSRuntime js)
        {
            _http = http;
            _js = js;
        }

        public async Task<List<ActivityMonthMarkerDto>> GetMonthMarkersAsync(int year, int month, string? province = null)
        {
            var url = $"api/activity/month-markers?year={year}&month={month}";
            if (!string.IsNullOrEmpty(province)) url += $"&province={province}";
            return await _http.GetFromJsonAsync<List<ActivityMonthMarkerDto>>(url) ?? new();
        }

        public async Task<List<ActivityDto>> GetDayAsync(DateTime date, string? province = null)
        {
            var url = $"api/activity/day?date={date:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(province)) url += $"&province={province}";
            return await _http.GetFromJsonAsync<List<ActivityDto>>(url) ?? new();
        }

        public async Task<ActivityDto?> CreateAsync(ActivityUpsertDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/activity", dto);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<ActivityDto>()
                : null;
        }

        public async Task<ActivityDto?> UpdateAsync(ActivityUpsertDto dto)
        {
            var response = await _http.PutAsJsonAsync("api/activity", dto);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<ActivityDto>()
                : null;
        }

        public Task<HttpResponseMessage> DeleteAsync(int id) => _http.DeleteAsync($"api/activity/{id}");

        public async Task ConnectAsync(string hubUrl)
        {
            if (_hubConnection != null && _hubConnection.State != HubConnectionState.Disconnected)
                return;

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                        await _js.InvokeAsync<string?>("localStorage.getItem", "authToken");
                })
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)
                })
                .Build();

            _hubConnection.On<ActivityDto>("ActivityStarting", dto => ActivityStarting?.Invoke(dto));
            _hubConnection.On<ActivityDto>("ActivityChanged", dto => ActivityChanged?.Invoke(dto));
            _hubConnection.On<int>("ActivityDeleted", id => ActivityDeleted?.Invoke(id));

            await _hubConnection.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();
        }
        public async Task<List<ActivityDto>> GetDayMyRegionAsync(DateTime date)
        {
            var url = $"api/activity/day/my-region?date={date:yyyy-MM-dd}";
            return await _http.GetFromJsonAsync<List<ActivityDto>>(url) ?? new();
        }
    }
}