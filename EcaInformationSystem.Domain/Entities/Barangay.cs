namespace EcaInformationSystem.Domain.Entities
{
    public class Barangay
    {
        public int Id { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public int PsgcCodeBarangay { get; set; }
        public string? Name { get; set; }
    }
}
