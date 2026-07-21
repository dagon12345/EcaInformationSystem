using EcaInformationSystem.Api.BackgroundServices;
using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs.Activity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/activity")]
    [Authorize]
    public class ActivityController : ControllerBase
    {
        private readonly IActivityService _service;
        private readonly IHubContext<ActivityHub> _hub;
        private readonly IHubContext<PublicActivityHub> _publicHub;
        private readonly ActivityReminderScheduler _scheduler;

        public ActivityController(
            IActivityService service,
            IHubContext<ActivityHub> hub,
            IHubContext<PublicActivityHub> publicHub,
            ActivityReminderScheduler scheduler)
        {
            _service = service;
            _hub = hub;
            _publicHub = publicHub;
            _scheduler = scheduler;
        }

        [HttpGet("month-markers")]
        public async Task<ActionResult<List<ActivityMonthMarkerDto>>> GetMonthMarkers(
            [FromQuery] int year, [FromQuery] int month, [FromQuery] string? province)
            => Ok(await _service.GetMonthMarkersAsync(year, month, province));

        [HttpGet("day")]
        public async Task<ActionResult<List<ActivityDto>>> GetDay(
            [FromQuery] DateTime date, [FromQuery] string? province)
            => Ok(await _service.GetActivitiesForDateAsync(date, province));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ActivityDto>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        // ── Public-facing, anonymous ────────────────────────────────────────
        [HttpGet("public/upcoming")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ActivityDto>>> GetPublicUpcoming([FromQuery] int take = 8)
            => Ok(await _service.GetPublicUpcomingAsync(take));

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<ActivityDto>> Create(ActivityUpsertDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var created = await _service.CreateAsync(dto, userId);

            _scheduler.Schedule(created);
            await _hub.Clients.All.SendAsync("ActivityChanged", created);

            if (created.IsPublic)
                await _publicHub.Clients.All.SendAsync("PublicActivityChanged", created);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<ActivityDto>> Update(ActivityUpsertDto dto)
        {
            // Capture prior public state BEFORE mutating, so we can detect a
            // public → private transition and tell public viewers to remove it.
            var before = dto.Id.HasValue ? await _service.GetByIdAsync(dto.Id.Value) : null;
            var wasPublic = before?.IsPublic ?? false;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var updated = await _service.UpdateAsync(dto, userId);
            if (updated is null) return NotFound();

            _scheduler.Schedule(updated);
            await _hub.Clients.All.SendAsync("ActivityChanged", updated);

            if (updated.IsPublic)
            {
                // Still (or newly) public — push the update/creation to public viewers
                await _publicHub.Clients.All.SendAsync("PublicActivityChanged", updated);
            }
            else if (wasPublic && !updated.IsPublic)
            {
                // Was public, just got unchecked — tell public viewers to remove it
                await _publicHub.Clients.All.SendAsync("PublicActivityDeleted", updated.Id);
            }

            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _service.GetByIdAsync(id);
            var wasPublic = existing?.IsPublic ?? false;

            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound();

            _scheduler.Cancel(id);
            await _hub.Clients.All.SendAsync("ActivityDeleted", id);

            if (wasPublic)
                await _publicHub.Clients.All.SendAsync("PublicActivityDeleted", id);

            return NoContent();
        }
    }
}