namespace EcaInformationSystem.Api.ZkDevice
{
    public class ZkEnrolledUser
    {
        public string BiometricUserId { get; set; } = string.Empty; // PIN
        public string Name { get; set; } = string.Empty;
        public int Privilege { get; set; }
        public string? CardNumber { get; set; }
    }
}
