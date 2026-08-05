namespace EcaInformationSystem.Api.ZkDevice
{
    public class ZkAttendanceRecord
    {
        public string BiometricUserId { get; set; } = string.Empty;
        public DateTime PunchTime { get; set; }
        public int Status { get; set; }
        public int VerifyMode { get; set; }
    }
}
