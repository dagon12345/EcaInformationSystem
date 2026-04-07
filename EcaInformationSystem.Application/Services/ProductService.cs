using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _repo;

        public ProductService(IProductRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<ProductDto>> GetAllAsync()
        {
            var products = await _repo.GetAllAsync();

            return products.Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price
            }).ToList();
        }

        public async Task<ProductDto?> GetByIdAsync(Guid id)
        {
            var p = await _repo.GetByIdAsync(id);
            if (p == null) return null;

            return new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price
            };
        }

        public async Task<ProductDto> CreateAsync(CreateProductDto dto)
        {
            var product = new Product(dto.Name, dto.Price);
            await _repo.AddAsync(product);
            await _repo.SaveChangesAsync();

            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price
            };
        }

        public async Task UpdateAsync(Guid id, UpdateProductDto dto)
        {
            var product = await _repo.GetByIdAsync(dto.Id);
            if (product == null)
                throw new Exception("Product not found.");

            product.Update(dto.Name, dto.Price);

            await _repo.UpdateAsync(product);
            await _repo.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var product = await _repo.GetByIdAsync(id);
            if (product == null)
                throw new Exception("Product not found.");

            await _repo.DeleteAsync(product);
            await _repo.SaveChangesAsync();
        }
    }
}
