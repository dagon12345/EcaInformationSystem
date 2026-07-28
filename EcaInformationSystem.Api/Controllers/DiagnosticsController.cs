// Api/Controllers/DiagnosticsController.cs
//
// TEMPORARY — delete this file once you've confirmed the Substring
// translation behavior. This is not meant to ship; it exists purely to
// answer one question: does EF Core translate the conditional Substring()
// projection to server-side SQL SUBSTRING, or does it pull the full column
// and truncate client-side?

using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/diagnostics")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DiagnosticsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("substring-sql-check")]
        public IActionResult CheckSubstringTranslation()
        {
            var sql = _context.BeneficiaryInformations
                .Where(b => !b.IsDeleted)
                .Select(b => new
                {
                    b.Id,
                    EligibilityRemarksPreview = b.EligibilityRemarks != null && b.EligibilityRemarks.Length > 80
                        ? b.EligibilityRemarks.Substring(0, 80)
                        : b.EligibilityRemarks,
                    AssessmentRemarksPreview = b.AssessmentRemarks != null && b.AssessmentRemarks.Length > 80
                        ? b.AssessmentRemarks.Substring(0, 80)
                        : b.AssessmentRemarks
                })
                .Take(5)
                .ToQueryString();

            // Return as plain text so it's readable directly in the browser
            // or Swagger UI, rather than JSON-escaped with \r\n everywhere.
            return Content(sql, "text/plain");
        }
    }
}