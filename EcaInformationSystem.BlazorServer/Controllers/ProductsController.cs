using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.BlazorServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _service;
        public ProductsController(IProductService service)
        {
            _service = service;
        }
        [HttpGet]
        public async Task<ActionResult<List<ProductDto>>> Get()
        {
            var products = await _service.GetAllAsync();
            return Ok(products);
        }
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProductDto>> Get(Guid id)
        {
            var product = await _service.GetByIdAsync(id);
            if (product == null) return NotFound();
            return Ok(product);
        }
        [HttpPost]
        public async Task<ActionResult<ProductDto>> Post([FromBody] CreateProductDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateProductDto dto)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch.");
            try
            {
                await _service.UpdateAsync(id, dto);
                return NoContent();
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }
        }
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _service.DeleteAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
