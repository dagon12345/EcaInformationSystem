using EcaInformationSystem.Api.ZkDevice;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Application.Services;
using EcaInformationSystem.Shared.DTOs.Dtr;
using EcaInformationSystem.Shared.DTOs.UserManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/dtr")]
    [Authorize]
    public class DtrController : ControllerBase
    {
        private readonly IDtrService _dtrService;
        private readonly IUserManagementService _userManagementService;
        private readonly IBiometricDeviceUserService _biometricDeviceUserService;
        private readonly IBiometricSyncStatusService _syncStatusService;
        private readonly IBiometricDeviceSettingService _deviceSettingService;
        private readonly IDtrDayMarkService _dayMarkService;
        private readonly IAttendanceLogService _attendanceLogService;
        private readonly IDtrPunchRequestService _punchRequestService;
        private readonly ZkSyncRunner _syncRunner;
        private readonly ZkDirectOptions _zkOptions;

        public DtrController(
            IDtrService dtrService,
            IUserManagementService userManagementService,
            IBiometricDeviceUserService biometricDeviceUserService,
            IBiometricSyncStatusService syncStatusService,
            IBiometricDeviceSettingService deviceSettingService,
            IDtrDayMarkService dayMarkService,
            IAttendanceLogService attendanceLogService,
            IDtrPunchRequestService punchRequestService,
            ZkSyncRunner syncRunner,
            IOptions<ZkDirectOptions> zkOptions)
        {
            _dtrService = dtrService;
            _userManagementService = userManagementService;
            _biometricDeviceUserService = biometricDeviceUserService;
            _syncStatusService = syncStatusService;
            _deviceSettingService = deviceSettingService;
            _dayMarkService = dayMarkService;
            _attendanceLogService = attendanceLogService;
            _punchRequestService = punchRequestService;
            _syncRunner = syncRunner;
            _zkOptions = zkOptions.Value;
        }

        // Only these three roles may mutate AttendanceLog directly — everyone
        // else's Add/Remove goes through DtrPunchRequestService as a pending
        // request instead (see AddManualPunch/RemoveManualPunch below).
        private bool IsPunchApprover() => DtrPunchRequestService.ApproverRoles.Any(User.IsInRole);

        // Every account — including SuperAdmin/Finance — has its own Region
        // claim and is scoped to it; there's no "sees every region" bypass
        // anywhere else in this codebase (WfpEca, AnnualGranteeTarget follow
        // the same rule), so the biometric device feature matches that.
        private int? GetRegionCodeFromClaims()
        {
            var raw = User.FindFirst("Region")?.Value;
            return int.TryParse(raw, out var code) ? code : null;
        }

        // ── Admin/Finance: the device's saved LAN address — this is what the
        // local sync tool actually connects to. Stored in the DB (not
        // appsettings) so it can be changed here, from anywhere, without
        // anyone touching config files or restarting the local instance.
        // One row per region — a region's admins only ever see/edit their
        // own office's device config. ───────────────────────────────────
        [HttpGet("device-settings")]
        [Authorize(Roles = "SuperAdmin,Finance")]
        public async Task<IActionResult> GetDeviceSettings()
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest(new { message = "No region is assigned to this account." });

            return Ok(await _deviceSettingService.GetAsync(regionCode.Value));
        }

        [HttpPut("device-settings")]
        [Authorize(Roles = "SuperAdmin,Finance")]
        public async Task<IActionResult> SaveDeviceSettings([FromBody] SaveDeviceSettingRequestDto dto)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest(new { message = "No region is assigned to this account." });

            if (string.IsNullOrWhiteSpace(dto.DeviceHost) || string.IsNullOrWhiteSpace(dto.DeviceSerialNumber))
                return BadRequest(new { message = "Device IP address and serial number are required." });

            return Ok(await _deviceSettingService.SaveAsync(dto, regionCode.Value));
        }

        // ── Admin/Finance: "Connect" button — cheap reachability check
        // against this region's saved device IP. Only ever succeeds when
        // called against an instance that's (a) on the same LAN as the
        // physical device and (b) has ZkDirect:Enabled=true — true for the
        // LocalSync profile AND a plain local `dotnet watch` (Development
        // already has it enabled), never true for the deployed production
        // API, since it can never reach an office LAN device. Region comes
        // from the caller's own JWT, not a fixed local port — so whichever
        // instance you're actually running against, it works the same way. ──
        [HttpPost("test-connection")]
        [Authorize(Roles = "SuperAdmin,Finance")]
        public async Task<IActionResult> TestConnection(CancellationToken ct)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest(new { message = "No region is assigned to this account." });

            if (!_zkOptions.Enabled)
            {
                return Ok(new ConnectionTestResultDto
                {
                    Success = false,
                    ErrorMessage = "ZkDirect is disabled on this instance. This only works when run on a machine with LAN access to the device (LocalSync profile, or a local dotnet watch with ZkDirect enabled)."
                });
            }

            return Ok(await _syncRunner.TestConnectionAsync(regionCode.Value, ct));
        }

        // ── Admin/Finance: "Sync DTR" button — same reachability rules as
        // test-connection above. ─────────────────────────────────────────
        [HttpPost("sync-now")]
        [Authorize(Roles = "SuperAdmin,Finance")]
        public async Task<IActionResult> SyncNow([FromBody] TriggerSyncRequestDto dto, CancellationToken ct)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest(new { message = "No region is assigned to this account." });

            if (!_zkOptions.Enabled)
            {
                return Ok(new TriggerSyncResultDto
                {
                    Success = false,
                    ErrorMessage = "ZkDirect is disabled on this instance. This only works when run on a machine with LAN access to the device (LocalSync profile, or a local dotnet watch with ZkDirect enabled)."
                });
            }

            var syncedByName = string.IsNullOrWhiteSpace(dto.SyncedByName) ? null : dto.SyncedByName.Trim();
            return Ok(await _syncRunner.RunOnceAsync(regionCode.Value, syncedByName, ct));
        }

        // ── Admin/Finance: browse who's actually enrolled on the physical
        // device, scoped to this region's device only ─────────────────────
        [HttpGet("device-users")]
        [Authorize(Roles = "SuperAdmin,Finance")]
        public async Task<IActionResult> GetDeviceUsers()
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest(new { message = "No region is assigned to this account." });

            return Ok(await _biometricDeviceUserService.GetAllAsync(regionCode.Value));
        }

        // ── Admin/Finance: is this region's device actually reachable? ──────
        [HttpGet("sync-status")]
        [Authorize(Roles = "SuperAdmin,Finance")]
        public async Task<IActionResult> GetSyncStatus()
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest(new { message = "No region is assigned to this account." });

            return Ok(await _syncStatusService.GetAllAsync(regionCode.Value));
        }

        // ── Self-service: is attendance data likely stale? Drives the "please
        // connect to office WiFi and run the sync tool" banner — a device can't
        // push automatically, so there's no way to detect this from the
        // browser's side; staleness of the last successful sync is the best
        // proxy available. Scoped to the viewer's own region; no region on
        // the account just means "no status to show" rather than an error,
        // since every authenticated user hits this. ──────────────────────
        [HttpGet("sync-freshness")]
        public async Task<IActionResult> GetSyncFreshness()
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return Ok(new SyncFreshnessDto());

            return Ok(await _syncStatusService.GetFreshnessAsync(regionCode.Value));
        }

        // ── Self-service: any authenticated user views their own DTR ───────
        [HttpGet("me")]
        public async Task<IActionResult> GetMine([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var dtr = await _dtrService.GetForUserAsync(userId, start, end);
            if (dtr is null)
                return NotFound(new { message = "No biometric device is linked to your account yet. Ask SuperAdmin or Finance to assign one." });

            return Ok(dtr);
        }

        // ── Admin/Finance: list users for the biometric-assignment screen,
        // scoped to this admin's own region ─────────────────────────────────
        [HttpGet("users")]
        [Authorize(Roles = "SuperAdmin,Finance")]
        public async Task<IActionResult> GetUsers()
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest(new { message = "No region is assigned to this account." });

            return Ok(await _userManagementService.GetAllUsersAsync(regionCode.Value));
        }

        [HttpPost("users/{id:guid}/biometric-id")]
        [Authorize(Roles = "SuperAdmin,Finance")]
        public async Task<IActionResult> SetBiometricUserId(Guid id, [FromBody] SetBiometricUserIdRequestDto dto)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest(new { message = "No region is assigned to this account." });

            var target = await _userManagementService.GetUserByIdAsync(id);
            if (target is null)
                return NotFound();
            if (target.Region != regionCode)
                return Forbid();

            try
            {
                var setBy = User.Identity?.Name ?? "Unknown";
                await _userManagementService.SetBiometricUserIdAsync(id, dto.BiometricUserId, setBy);
                return Ok(new { message = "Biometric device link updated." });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        }

        // ── Admin/Finance: any user's DTR — restricted to their own region ──
        [HttpGet("user/{id:guid}")]
        [Authorize(Roles = "SuperAdmin,Finance")]
        public async Task<IActionResult> GetForUser(Guid id, [FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest(new { message = "No region is assigned to this account." });

            var target = await _userManagementService.GetUserByIdAsync(id);
            if (target is null)
                return NotFound();
            if (target.Region != regionCode)
                return Forbid();

            var dtr = await _dtrService.GetForUserAsync(id, start, end);
            return dtr is null ? NotFound() : Ok(dtr);
        }

        // ── Admin/Finance: all-users overview for a date span, scoped to
        // their own region ───────────────────────────────────────────────
        [HttpGet("all")]
        [Authorize(Roles = "SuperAdmin,Finance")]
        public async Task<IActionResult> GetAll([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest(new { message = "No region is assigned to this account." });

            return Ok(await _dtrService.GetAllSummariesAsync(start, end, regionCode.Value));
        }

        // ── WFH/Holiday/Note marks on the DTR print preview — a viewer can
        // always manage their OWN marks (My DTR); SuperAdmin/Finance can also
        // manage another same-region user's marks (Attendance Management's
        // "View DTR"). Same ownership rule for get/set/clear. ─────────────
        private async Task<bool> CanManageUserDtrAsync(Guid userId)
        {
            var selfIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (Guid.TryParse(selfIdClaim, out var selfId) && selfId == userId)
                return true;

            if (!(User.IsInRole("SuperAdmin") || User.IsInRole("Finance")))
                return false;

            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return false;

            var target = await _userManagementService.GetUserByIdAsync(userId);
            return target is not null && target.Region == regionCode;
        }

        [HttpGet("day-marks/{userId:guid}")]
        public async Task<IActionResult> GetDayMarks(Guid userId, [FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            if (!await CanManageUserDtrAsync(userId))
                return Forbid();

            return Ok(await _dayMarkService.GetForUserAsync(userId, start, end));
        }

        [HttpPut("day-marks")]
        public async Task<IActionResult> SetDayMark([FromBody] SetDtrDayMarkRequestDto dto)
        {
            if (!await CanManageUserDtrAsync(dto.UserId))
                return Forbid();

            if (dto.MarkType is not ("Wfh" or "Holiday" or "Note"))
                return BadRequest(new { message = "MarkType must be Wfh, Holiday, or Note." });

            if (dto.Slot is not (null or "AmIn" or "AmOut" or "PmIn" or "PmOut"))
                return BadRequest(new { message = "Slot must be AmIn, AmOut, PmIn, PmOut, or omitted." });

            if (dto.SlotEnd is not (null or "AmIn" or "AmOut" or "PmIn" or "PmOut"))
                return BadRequest(new { message = "SlotEnd must be AmIn, AmOut, PmIn, PmOut, or omitted." });

            if (dto.Slot is not null && dto.MarkType != "Note")
                return BadRequest(new { message = "Only a Note can be scoped to a column range — Wfh/Holiday are always whole-day." });

            if (dto.SlotEnd is not null && dto.Slot is null)
                return BadRequest(new { message = "SlotEnd requires a Slot to start the range from." });

            if (dto.Slot is not null && DtrSlotOrder.IndexOf(dto.SlotEnd ?? dto.Slot) < DtrSlotOrder.IndexOf(dto.Slot))
                return BadRequest(new { message = "SlotEnd must come at or after Slot (AmIn → AmOut → PmIn → PmOut)." });

            var updatedByName = User.Identity?.Name;
            await _dayMarkService.SetAsync(dto.UserId, dto.Date, dto.MarkType, dto.NoteText, dto.Slot, dto.SlotEnd, updatedByName);
            return Ok();
        }

        [HttpDelete("day-marks/{userId:guid}")]
        public async Task<IActionResult> ClearDayMark(Guid userId, [FromQuery] DateTime date, [FromQuery] string? slot = null)
        {
            if (!await CanManageUserDtrAsync(userId))
                return Forbid();

            if (slot is not (null or "AmIn" or "AmOut" or "PmIn" or "PmOut"))
                return BadRequest(new { message = "Slot must be AmIn, AmOut, PmIn, PmOut, or omitted." });

            await _dayMarkService.ClearAsync(userId, date, slot);
            return Ok();
        }

        // ── Self-service: fill in a time in/out the employee forgot to
        // punch — this is the log book's whole basis, so a user must be
        // able to correct their own missed punch, not just SuperAdmin/
        // Finance. Same ownership rule as day-marks above: a viewer can
        // always manage their OWN record; SuperAdmin/Finance can also
        // manage another same-region user's.
        //
        // ✅ NEW — only Admin/Finance/SuperAdmin actually write the punch
        // here. Everyone else's request is filed as Pending instead (see
        // DtrPunchRequestService) and notified to those three roles via
        // UnifiedNotificationBell — the punch only takes effect once one of
        // them approves it through the pending-requests screen. ──────────
        [HttpPost("manual-punch")]
        public async Task<IActionResult> AddManualPunch([FromBody] AddManualPunchRequestDto dto)
        {
            if (!await CanManageUserDtrAsync(dto.UserId))
                return Forbid();

            var target = await _userManagementService.GetUserByIdAsync(dto.UserId);
            if (target is null)
                return NotFound();
            if (string.IsNullOrWhiteSpace(target.BiometricUserId))
                return BadRequest(new { message = "This user has no biometric device ID linked yet." });

            var addedByName = User.Identity?.Name ?? "Unknown";

            if (IsPunchApprover())
            {
                var id = await _attendanceLogService.AddManualPunchAsync(target.BiometricUserId, dto.PunchDateTime, addedByName);
                return Ok(new DtrPunchActionResultDto { Pending = false, Id = id });
            }

            await _punchRequestService.RequestAddAsync(dto.UserId, target.BiometricUserId, target.FullName, dto.PunchDateTime, addedByName, target.Region);
            return Ok(new DtrPunchActionResultDto
            {
                Pending = true,
                Message = "Your time entry request has been submitted for approval."
            });
        }

        // A punch (device-synced or manual) is only ever addressed by its
        // AttendanceLog id (not a UserId), so ownership here means "the
        // log's BiometricUserId belongs to a user this caller may manage" —
        // same self-or-region-admin rule as everywhere else on this
        // controller.
        private async Task<bool> CanManagePunchOwnerAsync(string biometricUserId)
        {
            var selfIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (Guid.TryParse(selfIdClaim, out var selfId))
            {
                var self = await _userManagementService.GetUserByIdAsync(selfId);
                if (self is not null && self.BiometricUserId == biometricUserId)
                    return true;
            }

            if (!(User.IsInRole("SuperAdmin") || User.IsInRole("Finance")))
                return false;

            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return false;

            var regionUsers = await _userManagementService.GetAllUsersAsync(regionCode.Value);
            return regionUsers.Any(u => u.BiometricUserId == biometricUserId);
        }

        // Clears any punch (device-synced or manual) so it can be re-entered
        // via AddManualPunch above — the log book is authoritative, so a
        // wrong or missing device scan is just as correctable as a gap.
        //
        // ✅ NEW — same Admin/Finance/SuperAdmin-only direct-write split as
        // AddManualPunch: everyone else's removal is filed as a Pending
        // request instead of actually deleting the punch, so a request that
        // later gets rejected hasn't lost any real data in the meantime.
        [HttpDelete("manual-punch/{id:int}")]
        public async Task<IActionResult> RemoveManualPunch(int id)
        {
            var biometricUserId = await _attendanceLogService.GetPunchOwnerBiometricUserIdAsync(id);
            if (biometricUserId is null)
                return NotFound(new { message = "No time entry found with that Id." });

            if (!await CanManagePunchOwnerAsync(biometricUserId))
                return Forbid();

            if (IsPunchApprover())
            {
                var removed = await _attendanceLogService.RemovePunchAsync(id);
                return removed
                    ? Ok(new DtrPunchActionResultDto { Pending = false })
                    : NotFound(new { message = "No time entry found with that Id." });
            }

            var selfIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!Guid.TryParse(selfIdClaim, out var selfUserId))
                return Unauthorized();

            var self = await _userManagementService.GetUserByIdAsync(selfUserId);
            var punchTime = await _attendanceLogService.GetPunchTimeAsync(id);
            if (self is null || punchTime is null)
                return NotFound(new { message = "No time entry found with that Id." });

            var requestedByName = User.Identity?.Name ?? "Unknown";
            await _punchRequestService.RequestRemoveAsync(
                selfUserId, biometricUserId, self.FullName, id,
                punchTime.Value.ToString("h:mm tt"), punchTime.Value, requestedByName, self.Region);

            return Ok(new DtrPunchActionResultDto
            {
                Pending = true,
                Message = "Your time entry removal request has been submitted for approval."
            });
        }

        // ── Admin/Finance/SuperAdmin: pending self-service punch-edit
        // requests awaiting approval — feeds UnifiedNotificationBell and the
        // approval screen. Empty list for any other role (enforced inside
        // the service, mirroring LivenessCheckService.GetPendingReviewsForUserAsync). ──
        [HttpGet("punch-requests/pending")]
        public async Task<IActionResult> GetPendingPunchRequests()
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
            return Ok(await _punchRequestService.GetPendingForRoleAsync(role));
        }

        [HttpPost("punch-requests/{id:guid}/approve")]
        [Authorize(Roles = "Admin,Finance,SuperAdmin")]
        public async Task<IActionResult> ApprovePunchRequest(Guid id, [FromBody] ReviewDtrPunchRequestDto dto)
        {
            var reviewedByName = User.Identity?.Name ?? "Unknown";
            try
            {
                await _punchRequestService.ApproveAsync(id, reviewedByName, dto.Notes);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("punch-requests/{id:guid}/reject")]
        [Authorize(Roles = "Admin,Finance,SuperAdmin")]
        public async Task<IActionResult> RejectPunchRequest(Guid id, [FromBody] ReviewDtrPunchRequestDto dto)
        {
            var reviewedByName = User.Identity?.Name ?? "Unknown";
            try
            {
                await _punchRequestService.RejectAsync(id, reviewedByName, dto.Notes);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
