using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Shared.DTOs.ApplicationTracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/application-tracking")]
    [Authorize]
    public class ApplicationTrackingController : ControllerBase
    {
        private readonly IApplicationTrackingService _service;
        private readonly IHubContext<ApplicationTrackingHub> _hub;

        public ApplicationTrackingController(IApplicationTrackingService service, IHubContext<ApplicationTrackingHub> hub)
        {
            _service = service;
            _hub = hub;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("taggable-users")]
        public async Task<IActionResult> GetTaggableUsers() => Ok(await _service.GetTaggableUsersAsync());

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateApplicationBatchDto dto)
        {
            if (!User.IsInRole("Viewer") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
                return Forbid();

            try
            {
                var result = await _service.CreateAsync(dto, RequireUserId(), GetFullName(), GetRole());
                await NotifyIfHandedOffAsync(result);
                await BroadcastChangedAsync(result.Id);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Something changed at the same moment this was submitted. Please try again.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.InnerException?.Message ?? ex.Message);
            }
        }

        [HttpPost("{id:guid}/accept")]
        public async Task<IActionResult> Accept(Guid id)
            => await RunAsync(() => _service.AcceptAsync(id, RequireUserId(), GetFullName()));

        [HttpPost("{id:guid}/return-to-viewer")]
        public async Task<IActionResult> ReturnToViewer(Guid id, [FromBody] RelayApplicationNoteDto? dto)
            => await RunAsync(() => _service.ReturnToViewerAsync(id, RequireUserId(), GetFullName(), dto?.Note, dto?.IsFinding ?? false, dto?.FindingJustification, GetRole()), notifyOnHandoff: true);

        [HttpPost("{id:guid}/distribute-to-pdo")]
        public async Task<IActionResult> DistributeToPdo(Guid id, [FromBody] RelayApplicationDto dto)
            => await RunAsync(() => _service.DistributeToPdoAsync(id, RequireUserId(), GetFullName(), dto, GetRole()), notifyOnHandoff: true);

        [HttpPost("{id:guid}/endorse-to-finance")]
        public async Task<IActionResult> EndorseToFinance(Guid id, [FromBody] RelayApplicationDto dto)
            => await RunAsync(() => _service.EndorseToFinanceAsync(id, RequireUserId(), GetFullName(), dto, GetRole()), notifyOnHandoff: true);

        [HttpPost("{id:guid}/return-to-pdo-findings")]
        public async Task<IActionResult> ReturnToPdoForFindings(Guid id, [FromBody] ReturnApplicationForFindingsDto dto)
            => await RunAsync(() => _service.ReturnToPdoForFindingsAsync(id, RequireUserId(), GetFullName(), dto), notifyOnHandoff: true);

        [HttpPost("{id:guid}/forward-to-viewer-scanning")]
        public async Task<IActionResult> ForwardToViewerForScanning(Guid id, [FromBody] RelayApplicationDto dto)
            => await RunAsync(() => _service.ForwardToViewerForScanningAsync(id, RequireUserId(), GetFullName(), dto, GetRole()), notifyOnHandoff: true);

        [HttpPost("{id:guid}/complete")]
        public async Task<IActionResult> Complete(Guid id, [FromBody] RelayApplicationNoteDto? dto)
            => await RunAsync(() => _service.CompleteAsync(id, RequireUserId(), GetFullName(), dto?.Note));

        [HttpPost("{id:guid}/rows/{rowId:guid}/resolve-finding")]
        public async Task<IActionResult> ResolveFinding(Guid id, Guid rowId)
            => await RunAsync(() => _service.ResolveFindingAsync(id, rowId, RequireUserId(), GetFullName(), User.IsInRole("SuperAdmin")));

        // Admin/SuperAdmin — correct a wrongly-tagged recipient (e.g. the Viewer
        // fat-fingered the "send to" picker) without disturbing the batch's stage.
        [HttpPost("{id:guid}/reassign-recipient")]
        public async Task<IActionResult> ReassignRecipient(Guid id, [FromBody] RelayApplicationDto dto)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
                return Forbid();

            return await RunAsync(() => _service.ReassignRecipientAsync(id, dto.ToUserId, RequireUserId(), GetFullName()), notifyOnHandoff: true);
        }

        // Correct a typo in a relay history entry's Note or finding
        // justification, without disturbing who it was sent to/from. Only
        // SuperAdmin can correct any entry (any side of the hand-off);
        // everyone else — including plain Admin — can only correct/clear an
        // entry they raised themselves — enforced in the service.
        [HttpPut("{id:guid}/transfers/{transferId:guid}")]
        public async Task<IActionResult> UpdateTransfer(Guid id, Guid transferId, [FromBody] UpdateApplicationTransferNoteDto dto)
        {
            return await RunAsync(() => _service.UpdateTransferAsync(id, transferId, dto, RequireUserId(), GetFullName(), User.IsInRole("SuperAdmin"), GetRole()));
        }

        // ── SuperAdmin-only overrides — full CRUD regardless of who currently
        // holds the batch or what stage the workflow is on. ────────────────

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!User.IsInRole("SuperAdmin")) return Forbid();

            try
            {
                await _service.DeleteAsync(id, GetFullName());
                await _hub.Clients.All.SendAsync("ApplicationBatchDeleted", id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("This document batch was already changed or removed. Please refresh.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.InnerException?.Message ?? ex.Message);
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateHeader(Guid id, [FromBody] UpdateApplicationBatchHeaderDto dto)
        {
            if (!User.IsInRole("SuperAdmin")) return Forbid();
            return await RunAsync(() => _service.UpdateHeaderAsync(id, dto, GetFullName()));
        }

        [HttpPost("{id:guid}/rows")]
        public async Task<IActionResult> AddRow(Guid id, [FromBody] CreateApplicationGranteeRowDto dto)
        {
            if (!User.IsInRole("SuperAdmin")) return Forbid();
            return await RunAsync(() => _service.AddRowAsync(id, dto, GetFullName()));
        }

        [HttpPut("{id:guid}/rows/{rowId:guid}")]
        public async Task<IActionResult> UpdateRow(Guid id, Guid rowId, [FromBody] CreateApplicationGranteeRowDto dto)
        {
            if (!User.IsInRole("SuperAdmin")) return Forbid();
            return await RunAsync(() => _service.UpdateRowAsync(id, rowId, dto, GetFullName()));
        }

        [HttpDelete("{id:guid}/rows/{rowId:guid}")]
        public async Task<IActionResult> DeleteRow(Guid id, Guid rowId)
        {
            if (!User.IsInRole("SuperAdmin")) return Forbid();
            return await RunAsync(() => _service.DeleteRowAsync(id, rowId, GetFullName()));
        }

        private async Task<IActionResult> RunAsync(Func<Task<ApplicationBatchDto>> action, bool notifyOnHandoff = false)
        {
            try
            {
                var result = await action();
                if (notifyOnHandoff)
                    await NotifyIfHandedOffAsync(result);
                await BroadcastChangedAsync(result.Id);
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
        // whoever the batch is now tagged to — skipped when the caller tagged
        // themselves (e.g. Complete) or the batch just reached its terminal state.
        private async Task NotifyIfHandedOffAsync(ApplicationBatchDto batch)
        {
            var callerId = RequireUserId();
            if (batch.CurrentHolderUserId == callerId) return;
            if (batch.CurrentStatus == (int)ApplicationTrackingStatus.Completed) return;

            await _hub.Clients.User(batch.CurrentHolderUserId.ToString()).SendAsync("ApplicationTagged", new ApplicationTaggedNotificationDto
            {
                BatchId = batch.Id,
                ProvinceName = batch.ProvinceName,
                MunicipalityName = batch.MunicipalityName,
                MilestoneYear = batch.MilestoneYear,
                StatusLabel = batch.CurrentStatusLabel,
                TaggedByName = GetFullName(),
                RelayedAt = DateTime.UtcNow
            });
        }

        // Broadcast to EVERYONE (not just the newly-tagged recipient) so any open
        // Application Tracking page reflects the change live — covers cases the
        // targeted "ApplicationTagged" notification doesn't, e.g. a PDO's own page
        // staying stale after Finance relays the batch onward, or a SuperAdmin
        // edit/delete that other viewers wouldn't otherwise hear about.
        private async Task BroadcastChangedAsync(Guid batchId)
        {
            await _hub.Clients.All.SendAsync("ApplicationBatchChanged", batchId);
        }

        private Guid RequireUserId()
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : throw new InvalidOperationException("Could not identify the current user.");
        }

        private string GetFullName() => User.FindFirst("FullName")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? "Unknown";

        // Each account has exactly one role claim — used to automatically tag
        // WHO (in what capacity) raised a batch/relay-level finding, without
        // requiring the user to pick/confirm it themselves.
        private string? GetRole() => User.FindFirst(ClaimTypes.Role)?.Value;
    }
}
