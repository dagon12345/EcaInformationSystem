using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Services;
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
        public BeneficiaryController(IBeneficiaryInformationService service)
            => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<BeneficiaryInformationDto>>> Get()
        {
            var list = await _service.GetAllAsync();
            return Ok(list);
        }
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] BeneficiaryFilterDto filter)
            => Ok(await _service.GetPaginatedAsync(filter));

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] BeneficiaryFilterDto filter)
            => Ok(await _service.GetSummaryAsync(filter));

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateBeneficiaryInformationDto dto)
        {
            var userName = User.Identity?.Name ?? "System";
            var result = await _service.CreateAsync(dto, userName);
            return Ok(result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id, [FromBody] BeneficiaryInformationDto dto)
        {
            await _service.UpdateAsync(id, dto, User.Identity?.Name ?? "System");
            return NoContent();
        }

        [HttpDelete("soft-delete/{id:guid}")]
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

        [HttpPost("import")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(BeneficiaryImportResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Import([FromForm] ImportBeneficiaryExcelRequestDto request)
        {
            using var stream = request.File.OpenReadStream();
            var result = await _service.ImportExcelAsync(
                stream, request.File.FileName, request.SheetName, User.Identity?.Name ?? "System");
            return Ok(result);
        }
        [HttpPost("import/sheets")]
        [Consumes("multipart/form-data")]
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
    }
}
