using static WebAPI.Controllers.JournalController;

namespace WebAPI.Models
{
    public class CreateJournalDto
    {
        public string? EntryNumber { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string? Description { get; set; }
        public List<CreateJournalLineDto> Lines { get; set; } = new();
    }
}
