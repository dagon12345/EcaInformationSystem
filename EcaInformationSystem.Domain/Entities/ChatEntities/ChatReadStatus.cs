namespace EcaInformationSystem.Domain.Entities.ChatEntities
{
    // ✅ Drives the unread badge: unread count = messages in RoomId
    // with SentAt > LastReadAt for this user. One row per (user, room) pair.
    public class ChatReadStatus
    {
        public Guid Id { get; set; }
        public Guid RoomId { get; set; }
        public Guid UserId { get; set; }
        public DateTime LastReadAt { get; set; }
    }
}
