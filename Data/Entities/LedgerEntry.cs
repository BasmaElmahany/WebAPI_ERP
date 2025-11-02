namespace WebAPI.Data.Entities
{
    public class LedgerEntry
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public DateTime Date { get; set; }
        public int? JournalEntryId { get; set; }
        public string? Description { get; set; }
        public decimal Debit { get; set; } = 0;
        public decimal Credit { get; set; } = 0;
        public decimal Balance { get; set; } = 0;
    }
}
