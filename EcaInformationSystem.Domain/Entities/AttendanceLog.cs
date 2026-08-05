namespace EcaInformationSystem.Domain.Entities
{
    public class AttendanceLog
    {
        public int Id { get; set; }

        // "PIN" on the ZKT device — the enrolled user's device ID, not our app UserId
        public string BiometricUserId { get; set; } = string.Empty;

        public string DeviceSerialNumber { get; set; } = string.Empty;

        public DateTime PunchTime { get; set; }

        // ZKT status codes: 0=CheckIn 1=CheckOut 2=BreakOut 3=BreakIn 4=OTIn 5=OTOut
        public int Status { get; set; }

        // ZKT verify mode: 1=Fingerprint 4=Card 15=Face ...
        public int VerifyMode { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Set when SuperAdmin/Finance manually added this punch (e.g. the
        // employee forgot to time in/out) instead of it coming from the
        // device. Flows through the same AM/PM/undertime calculation as any
        // real punch — this only exists so the UI can show which entries
        // were manually entered and let them be removed individually.
        public bool IsManualEntry { get; set; }
        public string? AddedByName { get; set; }
    }
}
