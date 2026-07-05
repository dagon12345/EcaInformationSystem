using EcaInformationSystem.Shared.DTOs.Chat;

namespace EcaInformationSystem.Shared.DTOs
{
    public class PagedOversightRoomsDto
    {
        public List<ChatRoomDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    }
}
