using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Exceptions;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.IO;
using System.Linq;

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

        // ── Excel import ──────────────────────────────────────────────────

        [HttpPost("import/sheets")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> GetImportSheets([FromForm] ImportSeniorCitizenDirectoryExcelSheetRequestDto request)
        {
            var fileError = ValidateExcelFile(request.File);
            if (fileError is not null)
                return BadRequest(fileError);

            try
            {
                using var stream = request.File.OpenReadStream();
                var sheetNames = await _service.GetExcelSheetNamesAsync(stream);
                if (!sheetNames.Any())
                    return BadRequest("The uploaded file has no worksheets.");

                return Ok(sheetNames);
            }
            catch (Exception)
            {
                return BadRequest("Could not read that file. Make sure it's a valid, uncorrupted .xlsx workbook.");
            }
        }

        [HttpPost("import/preview")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> PreviewImport(IFormFile file, [FromForm] string sheetName)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            var fileError = ValidateExcelFile(file);
            if (fileError is not null)
                return BadRequest(fileError);

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _service.PreviewImportAsync(stream, sheetName, regionCode.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("import/confirm")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> ConfirmImport(IFormFile file, [FromForm] string sheetName, [FromForm] string? skipRowsJson)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            var fileError = ValidateExcelFile(file);
            if (fileError is not null)
                return BadRequest(fileError);

            try
            {
                var skipRows = string.IsNullOrWhiteSpace(skipRowsJson)
                    ? new HashSet<int>()
                    : System.Text.Json.JsonSerializer.Deserialize<HashSet<int>>(skipRowsJson) ?? new HashSet<int>();

                var userName = User.Identity?.Name ?? "System";

                using var stream = file.OpenReadStream();
                var result = await _service.ConfirmImportAsync(stream, sheetName, regionCode.Value, userName, skipRows);

                foreach (var id in result.ImportedIds)
                {
                    await BroadcastAsync(id, "Created", userName, regionCode.Value);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Guards against non-Excel files, empty uploads, and oversized uploads before
        // they ever reach ClosedXML — a bad extension or a corrupted file otherwise
        // surfaces as an opaque exception deep inside the parsing/preview logic.
        private static string? ValidateExcelFile(IFormFile? file)
        {
            if (file is null || file.Length == 0)
                return "Please select a file to upload.";

            if (file.Length > 20 * 1024 * 1024)
                return "File is too large. The maximum upload size is 20 MB.";

            var extension = Path.GetExtension(file.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                return "Only .xlsx Excel files are supported.";

            return null;
        }

        [HttpGet("download-import-template")]
        public IActionResult DownloadImportTemplate()
        {
            var bytes = _service.GenerateImportTemplate();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"SeniorCitizenDirectory_Template_{DateTime.Now:yyyy-MM-dd}.xlsx");
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
                LswdoPosition = full.LswdoPosition,
                LswdoContactNumber = full.LswdoContactNumber,
                LswdoEmail = full.LswdoEmail,
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
