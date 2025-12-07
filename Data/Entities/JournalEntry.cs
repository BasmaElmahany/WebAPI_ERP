namespace WebAPI.Data.Entities
{
    public class JournalEntry
    {
        public int Id { get; set; }
        public string? EntryNumber { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public bool Posted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? PhotoUrl { get; set; }
    }
}
