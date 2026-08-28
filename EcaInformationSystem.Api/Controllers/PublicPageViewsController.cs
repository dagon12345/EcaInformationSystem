using EcaInformationSystem.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    // Simple hit counter for the public marketing pages (Features, About,
    // Regional Offices, Directory of Officials, Developer) — powers the
    // small "eye icon + count" badge on each. Anonymous by design: these
    // are the pages a visitor sees BEFORE logging in.
    [ApiController]
    [Route("api/public-page-views")]
    [AllowAnonymous]
    public class PublicPageViewsController : ControllerBase
    {
        private readonly IPublicPageViewRepository _repo;

        // Only these keys are ever accepted — an open-ended pageKey would let
        // a caller spam arbitrary rows into the table.
        private static readonly HashSet<string> AllowedPageKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "features", "about", "contact", "officials", "developer"
        };

        public PublicPageViewsController(IPublicPageViewRepository repo)
        {
            _repo = repo;
        }

        // Called once per page per browser session (the client dedupes via
        // localStorage before calling this) — increments and returns the new total.
        [HttpPost("{pageKey}")]
        public async Task<IActionResult> RecordView(string pageKey)
        {
            if (!AllowedPageKeys.Contains(pageKey)) return BadRequest("Unknown page.");
            return Ok(await _repo.IncrementAndGetCountAsync(pageKey.ToLowerInvariant()));
        }

        // Read-only fetch — used when the client already recorded a view
        // this session and just needs the current count to display.
        [HttpGet("{pageKey}")]
        public async Task<IActionResult> GetCount(string pageKey)
        {
            if (!AllowedPageKeys.Contains(pageKey)) return BadRequest("Unknown page.");
            return Ok(await _repo.GetCountAsync(pageKey.ToLowerInvariant()));
        }
    }
}
