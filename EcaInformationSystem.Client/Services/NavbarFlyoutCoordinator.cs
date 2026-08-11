namespace EcaInformationSystem.Client.Services
{
    // Messages (ChatWidget) and Sticky Notes (StickyNotesWidget) are two
    // independent floating navbar panels — nothing stopped both from being
    // open at once, and since each is positioned relative to its own toggle
    // button in the same corner of the navbar, two open panels visually stack
    // on top of each other. This coordinator lets each panel announce "I just
    // opened" so any other open panel can close itself in response.
    public class NavbarFlyoutCoordinator
    {
        public event Action<string>? OnOpened;

        public void NotifyOpened(string panelId) => OnOpened?.Invoke(panelId);
    }
}
