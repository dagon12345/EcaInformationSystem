using System.Text.Json;
using EcaInformationSystem.Api.Extensions;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Application.Services;
using EcaInformationSystem.Domain.Exceptions;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BeneficiaryController : ControllerBase
    {
        private readonly IBeneficiaryInformationService _service;
        private readonly IJurisdictionGuardService _jurisdictionGuardService;
        private readonly CrossmatchJobService _crossmatchJobs;
        private readonly IServiceScopeFactory _scopeFactory;
        public BeneficiaryController(
            IBeneficiaryInformationService service,
            IJurisdictionGuardService jurisdictionGuardService,
            CrossmatchJobService crossmatchJobs,
            IServiceScopeFactory scopeFactory)
        {
            _service = service;
            _jurisdictionGuardService = jurisdictionGuardService;
            _crossmatchJobs = crossmatchJobs;
            _scopeFactory = scopeFactory;
        }
        [HttpPost("payment-history/{historyId}/set-current")]
        public async Task<IActionResult> SetCurrentPaymentHistory(Guid historyId, [FromBody] SetCurrentPaymentHistoryRequestDto request)
        {
            var userName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Unknown";
            try
            {
                await _service.SetCurrentPaymentHistoryAsync(request.BeneficiaryId, historyId, userName);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpDelete("payment-history/{historyId:guid}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeletePaymentHistory(Guid historyId)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.DeletePaymentHistoryAsync(historyId, userName);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpGet("payment-history/{beneficiaryId:guid}")]
        public async Task<IActionResult> GetPaymentHistory(Guid beneficiaryId)
        {
            var result = await _service.GetPaymentHistoryAsync(beneficiaryId);
            return Ok(result);
        }

        [HttpPost("bulk-add-payment-history")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> BulkAddPaymentHistory([FromBody] BulkAddPaymentHistoryRequestDto request)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.BulkAddPaymentHistoryAsync(
                    request.Ids, request.PayrollQuarter, request.FiscalYear,
                    request.PaymentStatus, request.ModeOfPayment, request.PaymentDate,
                    request.Remarks, userName);
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

        [HttpPost("edit-payment-history")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> EditPaymentHistory([FromBody] EditPaymentHistoryRequestDto request)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.EditPaymentHistoryEntryAsync(
                    request.HistoryId, request.BeneficiaryId, request.PayrollQuarter,
                    request.FiscalYear, request.PaymentStatus, request.ModeOfPayment,
                    request.PaymentDate, request.Remarks, userName);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpPost("similar-names")]
        public async Task<IActionResult> SearchSimilarNames([FromBody] BeneficiaryFilterDto filter)
        {
            try
            {
                var result = await _service.SearchSimilarNamesAsync(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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

        [HttpPost("create")]
        [Authorize(Policy = "GranteeEncodeAccess")]
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

        [HttpPost("possible-duplicates/resolve")]
        public async Task<IActionResult> ResolveDuplicatePair([FromBody] ResolveDuplicatePairRequestDto request)
        {
            var userName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Unknown";
            var result = await _service.ResolveDuplicatePairAsync(request.Record1Id, request.Record2Id, request.Remarks, userName);
            return Ok(result);
        }

        [HttpPost("possible-duplicates/unresolve")]
        public async Task<IActionResult> UnresolveDuplicatePair([FromBody] UnresolveDuplicatePairRequestDto request)
        {
            var userName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Unknown";
            var result = await _service.UnresolveDuplicatePairAsync(request.Record1Id, request.Record2Id, userName);
            return Ok(result);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "GranteeEncodeAccess")] // Admin/SuperAdmin/Encoder unrestricted; PDO has jurisdiction restrictions (see JurisdictionGuardService)
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
            var userName = User.Identity?.Name ?? "System";
            var bytes = await _service.ExportFilteredAsTemplateAsync(filter, userName);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Beneficiaries_{DateTime.Now:yyyy-MM-dd}.xlsx");
        }
        [HttpPost("import/preview")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> PreviewImport(
     IFormFile file, [FromForm] string sheetName, [FromForm] string? correctionsJson = null)
        {
            var corrections = string.IsNullOrWhiteSpace(correctionsJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<int, Dictionary<string, string>>>(correctionsJson);

            using var stream = file.OpenReadStream();
            var result = await _service.PreviewImportAsync(stream, file.FileName, sheetName, corrections);
            return Ok(result);
        }
        // ✅ NEW — read-only crossmatch scan, run as a background job so large
        // files (tested up to 20k+ rows) don't time out the HTTP request. The
        // upload returns a job id immediately; poll crossmatch/status/{jobId}
        // for progress and the final result. Never touches the database.
        [HttpPost("crossmatch/start")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> StartCrossmatch(IFormFile file, [FromForm] string sheetName)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var fileName = file.FileName;

            var job = _crossmatchJobs.Create();

            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedService = scope.ServiceProvider.GetRequiredService<IBeneficiaryInformationService>();
                try
                {
                    using var stream = new MemoryStream(bytes);
                    var result = await scopedService.GetCrossmatchPreviewAsync(
                        stream, fileName, sheetName,
                        (processed, total) =>
                        {
                            job.Processed = processed;
                            job.Total = total;
                        },
                        job.Cts.Token);

                    job.Result = result;
                    job.Status = CrossmatchJobStatus.Completed;
                }
                catch (OperationCanceledException)
                {
                    job.Status = CrossmatchJobStatus.Cancelled;
                }
                catch (Exception ex)
                {
                    job.Error = ex.Message;
                    job.Status = CrossmatchJobStatus.Failed;
                }
            });

            return Ok(new CrossmatchJobStartResultDto { JobId = job.JobId });
        }

        [HttpGet("crossmatch/status/{jobId}")]
        [Authorize(Policy = "AdminOrPDO")]
        public IActionResult GetCrossmatchStatus(Guid jobId)
        {
            var job = _crossmatchJobs.Get(jobId);
            if (job is null) return NotFound();

            return Ok(new CrossmatchJobStatusDto
            {
                Status = job.Status.ToString(),
                Processed = job.Processed,
                Total = job.Total,
                Result = job.Status == CrossmatchJobStatus.Completed ? job.Result : null,
                Error = job.Error
            });
        }

        [HttpPost("crossmatch/cancel/{jobId}")]
        [Authorize(Policy = "AdminOrPDO")]
        public IActionResult CancelCrossmatch(Guid jobId)
        {
            return _crossmatchJobs.Cancel(jobId) ? Ok() : NotFound();
        }

        // ✅ NEW — exports selected crossmatch rows (New or Possible Match)
        // into an import-ready .xlsx so the user can fill in the remaining
        // details (location if unresolved, NCSC assessment, payment status,
        // etc.) and hand it to the Import tab later, instead of creating
        // records directly from the crossmatch modal.
        [HttpPost("crossmatch/export-template")]
        [Authorize(Policy = "AdminOrPDO")]
        public IActionResult ExportCrossmatchTemplate([FromBody] List<CrossmatchRowDto> rows, [FromQuery] string sheetName)
        {
            if (rows is null || rows.Count == 0)
                return BadRequest("No rows selected to export.");

            var bytes = _service.ExportCrossmatchRowsAsTemplate(rows, string.IsNullOrWhiteSpace(sheetName) ? "For Import" : sheetName);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
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
            [FromForm] int? refYear,       // ✅ nullable
            [FromForm] string? correctionsJson = null)
        {
            var skipRows = JsonSerializer.Deserialize<HashSet<int>>(skipRowsJson) ?? new();
            var corrections = string.IsNullOrWhiteSpace(correctionsJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<int, Dictionary<string, string>>>(correctionsJson);
            var userName = User.Identity?.Name ?? "System";

            using var stream = file.OpenReadStream();

            var result = await _service.ConfirmImportAsync(
                stream, file.FileName, sheetName, userName, skipRows,
                quarter, batch, refYear, corrections);

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
        [HttpPost("replace")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> ReplaceBeneficiary([FromBody] ReplacePaymentHistoryRequestDto dto)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.ReplaceBeneficiaryAsync(
                    dto.OutgoingPaymentHistoryId,
                    dto.IncomingPaymentHistoryId,
                    dto.ReplacementDate,
                    dto.Remarks,
                    userName);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpPost("undo-replacement/{historyId:guid}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UndoReplacement(Guid historyId)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.UndoReplacementAsync(historyId, userName);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpGet("replacement-lookup")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> SearchReplacementLookup([FromQuery] string? search, [FromQuery] Guid excludeId)
        {
            var result = await _service.SearchBeneficiaryLookupAsync(search, excludeId);
            return Ok(result);
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
        // PDO/Admin/Encoder confirms the "turned 80 + Filipino" auto-eligibility
        // flip for a batch of candidate grantees surfaced by the grid. Nothing
        // gets written until this is explicitly confirmed — the service
        // re-checks both the eligibility criteria and (for PDO only — Encoder
        // is unrestricted, same as Admin/SuperAdmin) jurisdiction against
        // fresh DB data, ignoring anything about the submitted ids it can't
        // independently verify. Uses "GranteeEncodeAccess" (not "AdminOrPDO")
        // specifically because that's the one policy that already includes
        // Encoder alongside Admin/PDO/SuperAdmin.
        [HttpPost("confirm-auto-eligibility")]
        [Authorize(Policy = "GranteeEncodeAccess")]
        public async Task<IActionResult> ConfirmAutoEligibility([FromBody] List<Guid> ids)
        {
            var userName = User.Identity?.Name ?? "System";
            var role = User.GetRole();
            var result = await _service.ConfirmAutoEligibilityAsync(ids, userName, role);
            return Ok(result);
        }

        // Known Duplicate history for one grantee — deliberately per-record
        // only (never a grid/list endpoint), so this never leaks into any
        // count or statistics feature.
        [HttpGet("duplicate-history/{id:guid}")]
        public async Task<IActionResult> GetDuplicateHistory(Guid id)
        {
            var result = await _service.GetDuplicateHistoryAsync(id);
            return Ok(result);
        }

        [HttpGet("global-duplicate-summary")]
        public async Task<IActionResult> GetGlobalDuplicateSummary()
        {
            var result = await _service.GetGlobalDuplicateSummaryAsync();
            return Ok(result);
        }

    }
}
