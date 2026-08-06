using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Exceptions;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Controllers
{
    // View: any authenticated role, but scoped to the caller's own region —
    // this is a multi-region system, so a Region XIII (CARAGA) account only
    // ever sees the CARAGA directory. Add/Edit/Delete: PDO, Admin, SuperAdmin
    // only (the "AdminOrPDO" policy) — everyone else is read-only.
    [ApiController]
    [Route("api/senior-citizen-directory")]
    [Authorize]
    public class SeniorCitizenDirectoryController : ControllerBase
    {
        private readonly ISeniorCitizenDirectoryService _service;
        private readonly IHubContext<SeniorCitizenDirectoryHub> _hub;

        public SeniorCitizenDirectoryController(ISeniorCitizenDirectoryService service, IHubContext<SeniorCitizenDirectoryHub> hub)
        {
            _service = service;
            _hub = hub;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            return Ok(await _service.GetAllAsync(regionCode.Value));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            var result = await _service.GetByIdAsync(id, regionCode.Value);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{id:guid}/history")]
        public async Task<IActionResult> GetHistory(Guid id)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            return Ok(await _service.GetHistoryAsync(id, regionCode.Value));
        }

        [HttpPost]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> Create([FromBody] UpsertSeniorCitizenDirectoryDto dto)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            try
            {
                var userName = User.Identity?.Name ?? "System";
                var result = await _service.CreateAsync(dto, userName, regionCode.Value);
                await BroadcastAsync(result.Id, "Created", userName, regionCode.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> Update([FromBody] UpsertSeniorCitizenDirectoryDto dto)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            try
            {
                var userName = User.Identity?.Name ?? "System";
                var result = await _service.UpdateAsync(dto, userName, regionCode.Value);
                await BroadcastAsync(result.Id, "Updated", userName, regionCode.Value);
                return Ok(result);
            }
            catch (ConcurrencyException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.DeleteAsync(id, userName, regionCode.Value);

                await _hub.Clients.Group(SeniorCitizenDirectoryHub.RegionGroupName(regionCode.Value.ToString()))
                    .SendAsync("DirectoryEntryChanged", new SeniorCitizenDirectoryChangeDto
                    {
                        Id = id,
                        ChangeType = "Deleted",
                        Entry = null,
                        ChangedBy = userName
                    });

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private int? GetRegionCodeFromClaims()
        {
            var raw = User.FindFirst("Region")?.Value;
            return int.TryParse(raw, out var code) ? code : null;
        }

        private async Task BroadcastAsync(Guid id, string changeType, string userName, int regionCode)
        {
            var full = await _service.GetByIdAsync(id, regionCode);
            var listItem = full is null ? null : new SeniorCitizenDirectoryListItemDto
            {
                Id = full.Id,
                PsgcCodeRegion = full.PsgcCodeRegion,
                PsgcCodeProvince = full.PsgcCodeProvince,
                PsgcCodeMunicipality = full.PsgcCodeMunicipality,
                RegionName = full.RegionName,
                ProvinceName = full.ProvinceName,
                MunicipalityName = full.MunicipalityName,
                IncomeClassification = full.IncomeClassification,
                SeniorCitizensPopulation = full.SeniorCitizensPopulation,
                LswdoName = full.LswdoName,
                OscaHeadName = full.OscaHeadName,
                MayorName = full.MayorName,
                HasSeniorCitizenCenter = full.HasSeniorCitizenCenter,
                HasCashIncentive = full.HasCashIncentive,
                HasVaopHelpDesk = full.HasVaopHelpDesk,
                UpdatedAt = full.UpdatedAt,
                UpdatedBy = full.UpdatedBy
            };

            await _hub.Clients.Group(SeniorCitizenDirectoryHub.RegionGroupName(regionCode.ToString()))
                .SendAsync("DirectoryEntryChanged", new SeniorCitizenDirectoryChangeDto
                {
                    Id = id,
                    ChangeType = changeType,
                    Entry = listItem,
                    ChangedBy = userName
                });
        }
    }
}
