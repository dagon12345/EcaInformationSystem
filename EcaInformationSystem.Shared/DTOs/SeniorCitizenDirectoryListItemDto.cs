namespace EcaInformationSystem.Shared.DTOs
{
    // Narrow grid-row projection — only what the directory table actually renders.
    public class SeniorCitizenDirectoryListItemDto
    {
        public Guid Id { get; set; }
        public int PsgcCodeRegion { get; set; }
        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public string? RegionName { get; set; }
        public string? ProvinceName { get; set; }
        public string? MunicipalityName { get; set; }
        public string? IncomeClassification { get; set; }
        public int? SeniorCitizensPopulation { get; set; }
        public string? LswdoName { get; set; }
        public string? LswdoPosition { get; set; }
        public string? LswdoContactNumber { get; set; }
        public string? LswdoEmail { get; set; }
        public string? OscaHeadName { get; set; }
        public string? MayorName { get; set; }
        public bool? HasSeniorCitizenCenter { get; set; }
        public bool? HasCashIncentive { get; set; }
        public bool? HasVaopHelpDesk { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
