using EcaInformationSystem.Application.DTOs;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IProductService
    {
        Task<List<ProductDto>> GetAllAsync();
        Task<ProductDto?> GetByIdAsync(Guid id);
        Task<ProductDto> CreateAsync(CreateProductDto dto);
        Task UpdateAsync(Guid id, UpdateProductDto dto);
        Task DeleteAsync(Guid id);
    }
}
