namespace EcaInformationSystem.Domain.Common.Enum
{
    // What kind of hand-off a DocumentRoute entry records.
    public enum DocumentRouteAction
    {
        Tagged = 0,      // Document created and initially tagged to a recipient
        Relayed = 1,     // Current holder forwarded it to any other user
        Returned = 2,    // Current holder sent it back to whoever handed it to them
        Completed = 3    // Current holder marked the document as done
    }
}
