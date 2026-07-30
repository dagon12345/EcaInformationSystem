namespace EcaInformationSystem.Domain.Entities
{
    // A published "what's new" release note, broadcast to every connected
    // client so users know a hard refresh (Ctrl+Shift+R) is needed to pick up
    // the new deploy — Blazor WASM assemblies are otherwise cached by the
    // browser and won't update on their own.
    public class SystemUpdateNotice
    {
        public Guid Id { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        // Newline-separated bullet points describing what changed.
        public string Changes { get; set; } = string.Empty;

        public DateTime PublishedAt { get; set; }
        public Guid PublishedByUserId { get; set; }
        public string PublishedByName { get; set; } = string.Empty;
    }
}
