using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [IgnoreAntiforgeryToken]
    public class ProductsController : ControllerBase
    {
        private readonly IProductRepository _repo;
        public ProductsController(IProductRepository repo) => _repo = repo;
        [HttpGet]
        public async Task<IActionResult> Get() => Ok(await _repo.GetAllAsync());
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var item = await _repo.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Product product)

        {
            await _repo.AddAsync(product);
            await _repo.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
        }
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] Product updated)

        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return NotFound();
            existing.Update(updated.Name, updated.Price);
            await _repo.UpdateAsync(existing);
            await _repo.SaveChangesAsync();
            return NoContent();
        }
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _repo.DeleteAsync(id);
            await _repo.SaveChangesAsync();
            return NoContent();
        }
    }
}
