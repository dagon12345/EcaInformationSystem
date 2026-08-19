using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Shared.DTOs.DocumentTracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/document-tracking")]
    [Authorize]
    public class DocumentTrackingController : ControllerBase
    {
        private readonly IDocumentTrackingService _service;
        private readonly IHubContext<DocumentTrackingHub> _hub;

        public DocumentTrackingController(IDocumentTrackingService service, IHubContext<DocumentTrackingHub> hub)
        {
            _service = service;
            _hub = hub;
        }

        [HttpGet]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
            => Ok(await _service.GetPagedAsync(page, pageSize, search));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("taggable-users")]
        public async Task<IActionResult> GetTaggableUsers() => Ok(await _service.GetTaggableUsersAsync());

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTrackedDocumentDto dto)
            => await RunAsync(() => _service.CreateAsync(dto, RequireUserId(), GetFullName(), GetRole()), notifyOnHandoff: true);

        [HttpPost("{id:guid}/accept")]
        public async Task<IActionResult> Accept(Guid id)
            => await RunAsync(() => _service.AcceptAsync(id, RequireUserId(), GetFullName()));

        [HttpPost("{id:guid}/relay")]
        public async Task<IActionResult> Relay(Guid id, [FromBody] RelayDocumentToDto dto)
            => await RunAsync(() => _service.RelayAsync(id, dto, RequireUserId(), GetFullName(), GetRole()), notifyOnHandoff: true);

        [HttpPost("{id:guid}/return")]
        public async Task<IActionResult> Return(Guid id, [FromBody] ReturnDocumentDto dto)
            => await RunAsync(() => _service.ReturnAsync(id, dto, RequireUserId(), GetFullName(), GetRole()), notifyOnHandoff: true);

        [HttpPost("{id:guid}/complete")]
        public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteDocumentDto dto)
            => await RunAsync(() => _service.CompleteAsync(id, dto, RequireUserId(), GetFullName()));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTrackedDocumentDto dto)
            => await RunAsync(() => _service.UpdateAsync(id, dto, RequireUserId(), User.IsInRole("SuperAdmin")));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("Finance"))
                return Forbid();

            try
            {
                await _service.DeleteAsync(id, GetRole());
                await _hub.Clients.All.SendAsync("DocumentDeleted", id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("This document was already changed or removed. Please refresh.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.InnerException?.Message ?? ex.Message);
            }
        }

        [HttpPut("{id:guid}/routes/{routeId:guid}")]
        public async Task<IActionResult> UpdateRoute(Guid id, Guid routeId, [FromBody] UpdateDocumentRouteNoteDto dto)
            => await RunAsync(() => _service.UpdateRouteAsync(id, routeId, dto, RequireUserId(), User.IsInRole("SuperAdmin")));

        private async Task<IActionResult> RunAsync(Func<Task<TrackedDocumentDto>> action, bool notifyOnHandoff = false)
        {
            try
            {
                var result = await action();
                if (notifyOnHandoff)
                    await NotifyIfHandedOffAsync(result);
                await BroadcastChangedAsync(result);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("This document was already updated by another action — please refresh and try again.");
            }
            catch (Exception ex)
            {
                // Surface the real cause instead of an opaque, bodyless 500 —
                // this is an internal admin tool, so it's safe to show the
                // message (and the often-more-useful EF InnerException) here.
                return StatusCode(500, ex.InnerException?.Message ?? ex.Message);
            }
        }

        // Pushes a real-time "you have a document to accept" notification to
        // whoever the document is now tagged to — skipped when the caller
        // tagged themselves or the document just reached its terminal state.
        private async Task NotifyIfHandedOffAsync(TrackedDocumentDto document)
        {
            var callerId = RequireUserId();
            if (document.CurrentHolderUserId == callerId) return;
            if (document.Status == (int)TrackedDocumentStatus.Completed) return;

            await _hub.Clients.User(document.CurrentHolderUserId.ToString()).SendAsync("DocumentTagged", new DocumentTaggedNotificationDto
            {
                DocumentId = document.Id,
                SerialNumber = document.SerialNumber,
                Title = document.Title,
                StatusLabel = document.StatusLabel,
                TaggedByName = GetFullName(),
                RelayedAt = DateTime.UtcNow
            });
        }

        // Broadcast to EVERYONE (not just the newly-tagged recipient) so any open
        // Document Tracking page reflects the change live.
        private async Task BroadcastChangedAsync(TrackedDocumentDto document)
        {
            await _hub.Clients.All.SendAsync("DocumentChanged", document);
        }

        private Guid RequireUserId()
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : throw new InvalidOperationException("Could not identify the current user.");
        }

        private string GetFullName() => User.FindFirst("FullName")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? "Unknown";

        private string? GetRole() => User.FindFirst(ClaimTypes.Role)?.Value;
    }
}
