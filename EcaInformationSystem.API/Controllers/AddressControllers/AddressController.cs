using EcaInformationSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers.AddressControllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AddressController(IAddressSearchService addressSearchService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<object>());

        try
        {
            var results = await addressSearchService.SearchAsync(q, cancellationToken);
            return Ok(results);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected (debounce cancelled the request) — not an error
            return StatusCode(499);
        }
    }
}
