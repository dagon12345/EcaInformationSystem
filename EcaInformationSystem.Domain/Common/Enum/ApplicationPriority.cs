namespace EcaInformationSystem.Domain.Common.Enum
{
    // How urgently a batch needs attention — purely informational, does not
    // affect the workflow/relay rules, just how it's flagged in the UI.
    public enum ApplicationPriority
    {
        Normal = 0,
        Priority = 1,
        Urgent = 2
    }
}
