using System.Text.Json;
using EcaInformationSystem.Api.Extensions;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Exceptions;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BeneficiaryController : ControllerBase
    {
        private readonly IBeneficiaryInformationService _service;
        private readonly IJurisdictionGuardService _jurisdictionGuardService;
        public BeneficiaryController(IBeneficiaryInformationService service, IJurisdictionGuardService jurisdictionGuardService)
        {
            _service = service;
            _jurisdictionGuardService = jurisdictionGuardService;
        }

        [HttpPost("bulk-assign-refnumber")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> BulkAssignRefNumber([FromBody] BulkUpdateRefNumberRequestDto dto)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.BulkAssignRefNumberAsync(
                    dto.Ids, dto.Quarter, dto.Batch, dto.RefYear, userName);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<BeneficiaryInformationDto>>> Get()
        {
            var list = await _service.GetAllAsync();
            return Ok(list);
        }
        [HttpGet("edit/{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id); // returns BeneficiaryInformationDto
            if (result is null) return NotFound();
            return Ok(result);
        }
        // Change this one line — method attribute and parameter binding only,
        // nothing else in the action changes.
        [HttpPost("paged")]// was: [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromBody] BeneficiaryFilterDto filter) // was: [FromQuery]
            => Ok(await _service.GetPaginatedAsync(filter));

        // New endpoint, additive — your existing "paged" endpoint stays untouched
        // so you can compare behavior/perf side-by-side before fully switching over.
        [HttpPost("paged-list")]
        public async Task<IActionResult> GetPagedList([FromBody] BeneficiaryFilterDto filter)
            => Ok(await _service.GetPagedListAsync(filter));

        [HttpPost("count-matching")]
        public async Task<IActionResult> CountMatching([FromBody] BeneficiaryFilterDto filter)
            => Ok(await _service.GetMatchingCountAsync(filter));

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] BeneficiaryFilterDto filter)
            => Ok(await _service.GetSummaryAsync(filter));

        [HttpPost("dashboard-summary")]
        public async Task<IActionResult> GetDashboardSummary([FromBody] BeneficiaryFilterDto filter)
            => Ok(await _service.GetDashboardSummaryAsync(filter));

        [HttpPost("create")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> Create([FromBody] CreateBeneficiaryInformationDto dto)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                var result = await _service.CreateAsync(dto, userName);

                //Soft duplicates found - return 409 with the matches so the client can show a modal
                if (result.RequiresConfirmation)
                    return Conflict(result);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = result.CreatedBeneficiary!.Id },
                    result.CreatedBeneficiary
                );
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        // BeneficiaryController.cs
        // ✅ Change from GET with single param to POST with full filter
        [HttpPost("possible-duplicates")]
        public async Task<IActionResult> GetPossibleDuplicates(
            [FromBody] BeneficiaryFilterDto filter)
        {
            try
            {
                var result = await _service.GetPossibleDuplicatesAsync(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "AdminOrPDO")] // Admin and PDO can edit but the PDO have jurisdiction restrictions
        public async Task<IActionResult> Update(Guid id, [FromBody] BeneficiaryInformationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userName = User.Identity?.Name ?? "Unknown";
            var role = User.GetRole();

            //Jurisdiction check
            var jurisdictionError = await _jurisdictionGuardService.CheckAsync(userName, role, dto.PsgcCodeMunicipality);

            if (jurisdictionError is not null)
                return StatusCode(403, jurisdictionError);

            try
            {
                await _service.UpdateAsync(id, dto, userName);
                return Ok();
            }
            catch (ConcurrencyException ex)
            {
                // ✅ 409 Conflict — client knows to reload
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpDelete("soft-delete/{id:guid}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.SoftDeleteAsync(id, User.Identity?.Name ?? "System");
            return NoContent();
        }
        [HttpGet("filter")]
        public async Task<ActionResult<List<BeneficiaryInformationDto>>> Filter([FromQuery] BeneficiaryFilterDto filter)
        {
            var result = await _service.FilterAsync(filter);
            return Ok(result);
        }
        [HttpPost("export")]
        public async Task<IActionResult> Export([FromBody] BeneficiaryFilterDto filter)
        {
            var bytes = await _service.ExportFilteredAsTemplateAsync(filter);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Beneficiaries_{DateTime.Now:yyyy-MM-dd}.xlsx");
        }
        [HttpPost("import/preview")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> PreviewImport(
     IFormFile file, [FromForm] string sheetName)
        {
            using var stream = file.OpenReadStream();
            var result = await _service.PreviewImportAsync(stream, file.FileName, sheetName);
            return Ok(result);
        }
        // BeneficiaryController.cs
        [HttpPost("import/confirm")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> ConfirmImport(
            IFormFile file,
            [FromForm] string sheetName,
            [FromForm] string skipRowsJson,
            [FromForm] int? quarter,       // ✅ nullable
            [FromForm] string? batch,      // ✅ nullable
            [FromForm] int? refYear)       // ✅ nullable
        {
            var skipRows = JsonSerializer.Deserialize<HashSet<int>>(skipRowsJson) ?? new();
            var userName = User.Identity?.Name ?? "System";

            using var stream = file.OpenReadStream();

            var result = await _service.ConfirmImportAsync(
                stream, file.FileName, sheetName, userName, skipRows,
                quarter, batch, refYear);

            return Ok(result);
        }
        [HttpPost("import/sheets")]
        [Consumes("multipart/form-data")]
        [Authorize(Policy = "AdminOrPDO")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetImportExcelSheets([FromForm] ImportBeneficiaryExcelSheetRequestDto request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                    return BadRequest("Please upload a valid Excel file.");

                using var stream = request.File.OpenReadStream();

                var sheets = await _service.GetExcelSheetNamesAsync(
                    stream,
                    request.File.FileName);

                return Ok(sheets);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpPost("update-excel")]
        [Consumes("multipart/form-data")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateFromExcel([FromForm] UpdateBeneficiaryExcelRequestDto request)
        {
            // 1. Validation
            if (request.File == null || request.File.Length == 0)
                return BadRequest("No file uploaded.");

            if (string.IsNullOrWhiteSpace(request.SheetName))
                return BadRequest("Sheet name is required.");

            try
            {
                // 2. Open the stream from the uploaded file
                using var stream = request.File.OpenReadStream();

                // 3. Get the username (from Auth or System)
                var userName = User.Identity?.Name ?? "System";

                // 4. Call your service method
                var result = await _service.UpdateExcelAsync(
                    stream,
                    request.File.FileName,
                    request.SheetName,
                    userName);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("download-import-template")]
        public IActionResult DownloadImportTemplate()
        {
            var bytes = _service.GenerateImportTemplate();
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ECAReS_ImportTemplate_{DateTime.Now:yyyy-MM-dd}.xlsx"
            );
        }
        [HttpGet("log-summary/{Id}")]
        public async Task<IActionResult> GetLogSummary(Guid Id)
        {
            var result = await _service.GetLogSummaryAsync(Id);
            return Ok(result);
        }
        /// <summary>
        /// Returns full beneficiary data for a given list of IDs.
        /// Used by the payroll modal to load records selected across multiple pages.
        /// </summary>
        [HttpPost("by-ids")]
        public async Task<IActionResult> GetByIds([FromBody] List<Guid> ids)
        {
            if (ids == null || ids.Count == 0)
                return Ok(new List<BeneficiaryInformationDto>());
            // Add a cap to GetByIds — independent of the repository's internal 500-chunk
            // batching, which protects SQL's parameter limit but not against an
            // unreasonable TOTAL request size.
            const int MaxIdsPerRequest = 5000;
            if (ids.Count > MaxIdsPerRequest)
                return BadRequest($"Cannot request more than {MaxIdsPerRequest} records in a single call.");

            var results = await _service.GetByIdsAsync(ids.Distinct().ToList());
            return Ok(results);
        }

        [HttpPost("bulk-payment-status")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> BulkUpdatePaymentStatus([FromBody] BulkUpdatePaymentStatusRequestDto request)
        {
            try
            {
                // Get the current logged-in user's name
                var userName = User.Identity?.Name ?? "System";

                await _service.BulkUpdatePaymentStatusAsync(
                     request.Ids,
                     request.PaymentStatus,
                     request.ModeOfPayment,
                     request.PaymentDate,
                     userName,
                     request.RowVersions);

                return Ok();
            }
            catch (ConcurrencyException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // ✅ NEW — independent of Payment Status entirely
        [HttpPost("bulk-payroll-quarter")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> BulkUpdatePayrollQuarter([FromBody] BulkUpdatePayrollQuarterRequestDto request)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.BulkUpdatePayrollQuarterAsync(
                    request.Ids,
                    request.PayrollQuarter,
                    userName,
                    request.RowVersions);
                return Ok();
            }
            catch (ConcurrencyException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpPost("bulk-co-status")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> BulkUpdateCoStatus([FromBody] BulkUpdateCoStatusRequestDto dto)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.BulkUpdateCoStatusAsync(
                    dto.Ids,
                    dto.CoStatus,
                    dto.CoDateEndorsed,
                    dto.CoDateApproved,
                    userName,
                    dto.RowVersions);
                return Ok();
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
        [HttpPost("bulk-eligibility-batchcode")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> BulkUpdateEligibilityAndBatchCode([FromBody] BulkUpdateEligibilityBatchCodeRequestDto request)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.BulkUpdateEligibilityAndBatchCodeAsync(request.Ids,
                request.IsEligible,
                request.BatchCode,
                userName,
                request.RowVersions);
                return Ok(new { message = "Bulk update successful" });
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
        [HttpGet("global-duplicate-summary")]
        public async Task<IActionResult> GetGlobalDuplicateSummary()
        {
            var result = await _service.GetGlobalDuplicateSummaryAsync();
            return Ok(result);
        }

    }
}
