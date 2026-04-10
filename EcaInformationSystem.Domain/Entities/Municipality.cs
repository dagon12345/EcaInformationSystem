namespace EcaInformationSystem.Domain.Entities
{
    public class Municipality
    {
        public int Id { get; set; }
        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public string? Name { get; set; }
    }
}
