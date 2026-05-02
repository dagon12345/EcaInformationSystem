namespace EcaInformationSystem.Application.DTOs
{
    public class BeneficiaryFilterDto
    {
        public int? PsgcCodeRegion { get; set; }
        public int? PsgcCodeProvince { get; set; }
        public int? PsgcCodeMunicipality { get; set; }
        public int? PsgcCodeBarangay { get; set; }

        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? FullName { get; set; }
        public int? SpecificAge { get; set; }
        public string? Validator { get; set; }
        public string? BatchCode { get; set; }
        public DateTime? SpecificBirthday { get; set; }
        public DateTime? BirthdayFrom { get; set; }
        public DateTime? BirthdayTo { get; set; }
        public bool? OnlyEightyYearsOld { get; set; }
        public int? MilestoneYear { get; set; }
        public int? Sex { get; set; }
        public int? PaymentStatus { get; set; }
        public DateTime? PaymentDate { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public List<int> PsgcCodeProvinces { get; set; } = new();
        public List<int> PsgcCodeMunicipalities { get; set; } = new();
        public List<int> PsgcCodeBarangays { get; set; } = new();

    }
}
