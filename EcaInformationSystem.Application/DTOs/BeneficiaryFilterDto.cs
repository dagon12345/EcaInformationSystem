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
        public int? SpecificAge { get; set; }
        public int? AgeFrom { get; set; }
        public int? AgeTo { get; set; }
        public DateTime? SpecificBirthday { get; set; }
        public DateTime? BirthdayFrom { get; set; }
        public DateTime? BirthdayTo { get; set; }
        public int? Sex { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
