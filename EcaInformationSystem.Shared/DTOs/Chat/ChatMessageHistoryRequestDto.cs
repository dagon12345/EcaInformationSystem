namespace EcaInformationSystem.Shared.DTOs.Chat
{
    // ✅ Reuses your existing pagination shape — same PagedResultDto<T> you already
    // use for beneficiaries/logs. Sorted newest-first; "before" cursor = oldest
    // message's SentAt in the current batch, used for the infinite-scroll-up fetch.
    public class ChatMessageHistoryRequestDto
    {
        public Guid RoomId { get; set; }
        public DateTime? Before { get; set; } // null = latest page
        public int PageSize { get; set; } = 30;
    }
}
