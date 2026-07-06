namespace EcaInformationSystem.Domain.Entities.ChatEntities
{
    // ✅ Only populated for Direct (1:1) rooms — exactly 2 rows per DM thread.
    // Regional/Global membership is NEVER stored here; it's computed at query time
    // ("user.Region == room.RegionCode" or "room.Type == Global"), so a brand-new
    // user is automatically "in" their region's room with zero backfill needed.
    public class ChatRoomMember
    {
        public Guid Id { get; set; }
        public Guid RoomId { get; set; }
        public Guid UserId { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime? ClearedAt { get; set; } // ✅ NEW — null = never cleared
        public ChatRoom Room { get; set; } = default!;
    }
}
