using System;
using System.Collections.Generic;
using System.Text;

namespace EcaInformationSystem.Shared.DTOs
{
    public class PaymentHistoryDeletedInfoDto
    {
        public Guid BeneficiaryId { get; set; }
        public int PaymentStatus { get; set; }
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; }
    }
}
