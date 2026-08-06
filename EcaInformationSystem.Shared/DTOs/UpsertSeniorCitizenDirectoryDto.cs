namespace EcaInformationSystem.Shared.DTOs
{
    // Write payload for both Create (Id == null) and Update (Id set).
    public class UpsertSeniorCitizenDirectoryDto
    {
        public Guid? Id { get; set; }

        public int PsgcCodeRegion { get; set; }
        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }

        public string? IncomeClassification { get; set; }
        public int? SeniorCitizensPopulation { get; set; }
        public string? PopulationAsOfNote { get; set; }

        public string? LswdoName { get; set; }
        public string? LswdoPosition { get; set; }
        public string? LswdoContactNumber { get; set; }
        public string? LswdoEmail { get; set; }

        public string? ScFocalName { get; set; }
        public string? ScFocalContactNumber { get; set; }
        public string? ScFocalEmail { get; set; }

        public string? OscaHeadName { get; set; }
        public string? OscaHeadLengthOfService { get; set; }
        public string? OscaHeadContactNumber { get; set; }
        public string? OscaHeadEmail { get; set; }

        public string? FscapPresidentName { get; set; }
        public string? FscapPresidentContactNumber { get; set; }
        public string? FscapPresidentEmail { get; set; }
        public string? FscapPresidentLengthOfService { get; set; }

        public bool? HasSeniorCitizenCenter { get; set; }
        public bool? IsSccAccredited { get; set; }
        public string? SccAccreditationValidity { get; set; }
        public string? WithoutSccResourcesNote { get; set; }
        public string? SccManagedBy { get; set; }
        public string? ServicesOffered { get; set; }

        public bool? HasCashIncentive { get; set; }
        public string? CashIncentiveDetails { get; set; }
        public bool? HasSupportingOrdinance { get; set; }
        public string? OrdinanceDocumentLinks { get; set; }

        public bool? HasVaopHelpDesk { get; set; }
        public string? VaopReferralMechanism { get; set; }

        public string? MayorName { get; set; }
        public string? MayorOfficeEmail { get; set; }

        // Required on update — omitted (null) on create.
        public byte[]? RowVersion { get; set; }
    }
}
