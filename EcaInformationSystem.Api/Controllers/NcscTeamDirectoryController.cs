using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Exceptions;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Controllers
{
    // View: any authenticated role — this directory is nationwide (not
    // region-scoped like Senior Citizen Directory), so a Caraga account can
    // still see the NCR/CAR/etc. contacts. Add/Edit/Delete/Import: Admin or
    // SuperAdmin only (the "AdminOnly" policy) — everyone else is read-only.
    [ApiController]
    [Route("api/ncsc-team-directory")]
    [Authorize]
    public class NcscTeamDirectoryController : ControllerBase
    {
        private readonly INcscTeamDirectoryService _service;
        private readonly IHubContext<NcscTeamDirectoryHub> _hub;

        public NcscTeamDirectoryController(INcscTeamDirectoryService service, IHubContext<NcscTeamDirectoryHub> hub)
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

        [HttpGet("{id:guid}/history")]
        public async Task<IActionResult> GetHistory(Guid id) => Ok(await _service.GetHistoryAsync(id));

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create([FromBody] UpsertNcscTeamDirectoryEntryDto dto)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                var result = await _service.CreateAsync(dto, userName);
                await BroadcastAsync(result.Id, "Created", userName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update([FromBody] UpsertNcscTeamDirectoryEntryDto dto)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                var result = await _service.UpdateAsync(dto, userName);
                await BroadcastAsync(result.Id, "Updated", userName);
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
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.DeleteAsync(id, userName);

                await _hub.Clients.All.SendAsync("DirectoryEntryChanged", new NcscTeamDirectoryChangeDto
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
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetImportSheets([FromForm] ImportNcscTeamDirectoryExcelSheetRequestDto request)
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
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> PreviewImport(IFormFile file, [FromForm] string sheetName)
        {
            var fileError = ValidateExcelFile(file);
            if (fileError is not null)
                return BadRequest(fileError);

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _service.PreviewImportAsync(stream, sheetName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("import/confirm")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> ConfirmImport(IFormFile file, [FromForm] string sheetName)
        {
            var fileError = ValidateExcelFile(file);
            if (fileError is not null)
                return BadRequest(fileError);

            try
            {
                var userName = User.Identity?.Name ?? "System";

                using var stream = file.OpenReadStream();
                var result = await _service.ConfirmImportAsync(stream, sheetName, userName);

                foreach (var id in result.ImportedIds)
                {
                    await BroadcastAsync(id, "Created", userName);
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
                $"NcscTeamDirectory_Template_{DateTime.Now:yyyy-MM-dd}.xlsx");
        }

        private async Task BroadcastAsync(Guid id, string changeType, string userName)
        {
            var full = await _service.GetByIdAsync(id);

            await _hub.Clients.All.SendAsync("DirectoryEntryChanged", new NcscTeamDirectoryChangeDto
            {
                Id = id,
                ChangeType = changeType,
                Entry = full,
                ChangedBy = userName
            });
        }
    }
}
