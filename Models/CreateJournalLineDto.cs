namespace WebAPI.Models
{
    public class CreateJournalLineDto
    {
        public int accountId { get; set; }
        public decimal debit { get; set; } = 0;
        public decimal credit { get; set; } = 0;
        public string? description { get; set; }
    }
}
