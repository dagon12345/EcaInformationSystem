using EcaInformationSystem.Shared.Models;

namespace EcaInformationSystem.Web.Services
{
    public class ProductService
    {
        private readonly HttpClient _http;
        public ProductService(HttpClient http)
        {
            _http = http;
        }
        public async Task<IEnumerable<ProductDto>?> GetProductsAsync()
        {
            return await _http.GetFromJsonAsync<IEnumerable<ProductDto>>("products");
        }
        public async Task<ProductDto?> GetProductByIdAsync(Guid id)
        {
            return await _http.GetFromJsonAsync<ProductDto>($"products/{id}");
        }
        public async Task<Guid?> CreateProductAsync(ProductDto dto)
        {
            var response = await _http.PostAsJsonAsync("products", dto);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<Guid>();
            return null;
        }
    }
}
