using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs.SystemUpdate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/system-updates")]
    // ✅ Same reasoning as SystemUpdateHub — Focal accounts need to read these
    // notices too (GetAll/GetLatest), so this can't be the bare [Authorize]
    // (→ DefaultPolicy, which excludes Focal). Publish stays SuperAdmin-only
    // via its own attribute below, unaffected by this class-level change.
    [Authorize(Policy = "AnyAuthenticatedIncludingFocal")]
    public class SystemUpdateController : ControllerBase
    {
        private readonly ISystemUpdateNoticeService _service;
        private readonly IHubContext<SystemUpdateHub> _hub;

        public SystemUpdateController(ISystemUpdateNoticeService service, IHubContext<SystemUpdateHub> hub)
        {
            _service = service;
            _hub = hub;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

        [HttpGet("latest")]
        public async Task<IActionResult> GetLatest()
        {
            var latest = await _service.GetLatestAsync();
            return latest is null ? NotFound() : Ok(latest);
        }

        // SuperAdmin only — suggested next version for the publish form
        // (auto-increments the patch segment of the latest notice). Purely a
        // convenience default; the admin can still type any version manually.
        [HttpGet("next-version")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetNextVersion() => Ok(new { version = await _service.GetNextVersionAsync() });

        // SuperAdmin only — publishes a release note and immediately pushes
        // it to every connected client via SignalR so open sessions see the
        // "refresh to update" banner without needing to reload first.
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Publish([FromBody] CreateSystemUpdateNoticeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Version) || string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest("Version and title are required.");

            var result = await _service.PublishAsync(dto, RequireUserId(), GetFullName());
            await _hub.Clients.All.SendAsync("NewSystemUpdate", result);
            return Ok(result);
        }

        private Guid RequireUserId()
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : throw new InvalidOperationException("Could not identify the current user.");
        }

        private string GetFullName() => User.FindFirst("FullName")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? "Unknown";
    }
}
