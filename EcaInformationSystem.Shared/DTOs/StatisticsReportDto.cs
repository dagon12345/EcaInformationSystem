namespace EcaInformationSystem.Shared.DTOs
{
    public class StatisticsReportDto
    {
        public int TotalBeneficiaries { get; set; }
        public int TotalMale { get; set; }
        public int TotalFemale { get; set; }
        public int PaidCount { get; set; }
        public int UnpaidCount { get; set; }
        public int PendingCount { get; set; }
        public int NotApplicableCount { get; set; }
        public decimal TotalDisbursement { get; set; }

        public int TotalAge80 { get; set; }
        public int TotalAge85 { get; set; }
        public int TotalAge90 { get; set; }
        public int TotalAge95 { get; set; }
        public int TotalAge100 { get; set; }
        public List<PayrollQuarterStatisticsDto> PayrollQuarterBreakdown { get; set; } = new();
        public List<AgeDistributionDto> AgeDistribution { get; set; } = new();
        public List<ProvinceStatisticsDto> ProvinceBreakdowns { get; set; } = new();
        public List<MunicipalityStatisticsDto> MunicipalityBreakdowns { get; set; } = new();
        public List<MilestoneYearSummaryDto> MilestoneYearSummary { get; set; } = new();
        public Dictionary<int, int> FiscalYearBreakdown { get; set; } = new();
    }
    public class PayrollQuarterStatisticsDto
    {
        public int Quarter { get; set; }
        public int Count { get; set; }
        public int PaidCount { get; set; }
        public decimal TotalDisbursement { get; set; }
    }
    public class MilestoneYearSummaryDto
    {
        public int Year { get; set; }
        public int TotalCount { get; set; }
        public int Age80Count { get; set; }
        public int Age85Count { get; set; }
        public int Age90Count { get; set; }
        public int Age95Count { get; set; }
        public int Age100Count { get; set; }
    }

    public class AgeDistributionDto
    {
        public int Age { get; set; }
        public int Count { get; set; }
    }

    public class ProvinceStatisticsDto
    {
        public string ProvinceName { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int Age80Count { get; set; }
        public int Age85Count { get; set; }
        public int Age90Count { get; set; }
        public int Age95Count { get; set; }
        public int Age100Count { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public decimal TotalDisbursement { get; set; }
        public int PaidCount { get; set; }
        public int UnpaidCount { get; set; }
        public int PendingCount { get; set; }
        public int NotApplicableCount { get; set; }
    }

    public class MunicipalityStatisticsDto
    {
        public string MunicipalityName { get; set; } = string.Empty;
        public string ProvinceName { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int Age80Count { get; set; }
        public int Age85Count { get; set; }
        public int Age90Count { get; set; }
        public int Age95Count { get; set; }
        public int Age100Count { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public decimal TotalDisbursement { get; set; }
    }
}
