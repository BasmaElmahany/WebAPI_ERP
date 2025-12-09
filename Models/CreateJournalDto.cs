using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json.Serialization;
using static WebAPI.Controllers.JournalController;

namespace WebAPI.Models
{
    public class CreateJournalDto
    {
        public string? EntryNumber { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string? Description { get; set; }
        public IFormFile? Photo { get; set; }

        // نستقبل الـ array كـ JSON-string وليس كمصفوفة
        public string? LinesJson { get; set; }
    }
}
