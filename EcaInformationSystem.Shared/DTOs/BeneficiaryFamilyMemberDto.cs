// Shared/DTOs/BeneficiaryFamilyMemberDto.cs
namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryFamilyMemberDto
    {
        public Guid Id { get; set; }
        public int RelationType { get; set; } // 1 = Spouse, 2 = Child
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        public string? ContactNumber { get; set; }
        public int? Sex { get; set; }
        public int? Age { get; set; }
        public bool IsLivingWithGrantee { get; set; }
        public int SortOrder { get; set; }
    }
}