namespace EcaInformationSystem.Shared.DTOs.Chat
{
    // ✅ Powers the @mention autocomplete dropdown — scoped to the CURRENT room's members
    public class ChatMemberSuggestionDto
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = default!;
        public string Role { get; set; } = default!;
    }
}
