namespace WebAPI.Models
{
    public class CreateJournalLineDto
    {
        public int AccountId { get; set; }
        public decimal Debit { get; set; } = 0;
        public decimal Credit { get; set; } = 0;
        public string? Description { get; set; }
    }
}
