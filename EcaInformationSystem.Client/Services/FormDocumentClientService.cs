using EcaInformationSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class FormDocumentClientService
    {
        private readonly HttpClient _http;

        public FormDocumentClientService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<FormDocumentDto>> GetAllAsync()
            => await _http.GetFromJsonAsync<List<FormDocumentDto>>("api/formdocument") ?? new();

        public async Task<(bool Success, string? Error)> UploadAsync(
            Stream fileStream, string fileName, string contentType,
            string title, string? description, string? category)
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            content.Add(streamContent, "file", fileName);
            content.Add(new StringContent(title), "title");
            if (!string.IsNullOrWhiteSpace(description)) content.Add(new StringContent(description), "description");
            if (!string.IsNullOrWhiteSpace(category)) content.Add(new StringContent(category), "category");

            var response = await _http.PostAsync("api/formdocument/upload", content);
            if (response.IsSuccessStatusCode) return (true, null);

            var error = await response.Content.ReadAsStringAsync();
            return (false, error);
        }

        public async Task<(bool Success, string? Error)> UpdateMetadataAsync(Guid id, FormDocumentUpdateDto dto)
        {
            var response = await _http.PutAsJsonAsync($"api/formdocument/{id}", dto);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(Guid id)
        {
            var response = await _http.DeleteAsync($"api/formdocument/{id}");
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        // Returns the raw bytes + suggested filename so the caller can trigger a browser download
        public async Task<(byte[] Data, string FileName, string ContentType)?> DownloadAsync(Guid id, string fallbackFileName)
        {
            var response = await _http.GetAsync($"api/formdocument/{id}/download");
            if (!response.IsSuccessStatusCode) return null;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? fallbackFileName;

            return (bytes, fileName.Trim('"'), contentType);
        }
    }
}