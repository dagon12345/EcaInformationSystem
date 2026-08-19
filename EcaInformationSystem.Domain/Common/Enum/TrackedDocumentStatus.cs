namespace EcaInformationSystem.Domain.Common.Enum
{
    // Overall lifecycle state of a TrackedDocument. Unlike ApplicationTrackingStatus,
    // this feature has no fixed pipeline of stages — a document stays InTransit
    // through any number of free-form hand-offs until whoever currently holds it
    // marks it Completed.
    public enum TrackedDocumentStatus
    {
        InTransit = 0,
        Completed = 1
    }
}
