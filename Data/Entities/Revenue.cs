namespace WebAPI.Data.Entities
{
    public class Revenue
    {
        public int Id { get; set; }
        public DateTime? Date { get; set; }
        public int? AccountId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }
}
