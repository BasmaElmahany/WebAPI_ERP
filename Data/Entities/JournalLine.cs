namespace WebAPI.Data.Entities
{
    public class JournalLine
    {
        public int Id { get; set; }
        public int JournalEntryId { get; set; }
        public int AccountId { get; set; }
        public decimal Debit { get; set; } = 0;
        public decimal Credit { get; set; } = 0;
        public string? Description { get; set; }
    }
}
