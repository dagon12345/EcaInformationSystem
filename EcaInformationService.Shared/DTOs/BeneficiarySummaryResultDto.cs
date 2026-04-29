namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiarySummaryResultDto
    {
        public List<BeneficiaryInformationDto> Beneficiaries { get; set; } = new();
        public int TotalBeneficiaries { get; set; }
        public int TotalMale { get; set; }
        public int TotalFemale { get; set; }

        public int Age80Count { get; set; }
        public int Age85Count { get; set; }
        public int Age90Count { get; set; }
        public int Age95Count { get; set; }
        public int Age100Count { get; set; }

        public List<ProvinceCountDto> ProvinceCounts { get; set; } = new();
        public List<MunicipalityCountDto> MunicipalityCounts { get; set; } = new();
    }

    public class ProvinceCountDto
    {
        public string Province{ get; set; } = string.Empty;
        public int Count { get; set; }
    }  
    public class MunicipalityCountDto
    {
        public string Municipality { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
