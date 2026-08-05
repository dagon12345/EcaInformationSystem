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
    }
}
