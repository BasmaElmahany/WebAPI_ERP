namespace WebAPI.Models
{
    public class UpdateJournalDto
    {
        public string EntryNumber { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }

        public IFormFile? Photo { get; set; }
        public string LinesJson { get; set; }
    }
}
