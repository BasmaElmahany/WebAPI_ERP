using Microsoft.AspNetCore.Mvc;
using WebAPI.Data.Entities;
using WebAPI.Data;
using WebAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Controllers
{
    [ApiController]
    [Produces("application/json")]
    [Route("api/{project}/journals")]
    public class JournalController : ControllerBase
    {
        private readonly ProjectDbContextFactory _factory;
        private readonly AccountingService _service;

        public JournalController(ProjectDbContextFactory factory, AccountingService service)
        {
            _factory = factory;
            _service = service;
        }

        public class CreateJournalDto
        {
            public string? EntryNumber { get; set; }
            public DateTime Date { get; set; } = DateTime.UtcNow;
            public string? Description { get; set; }
            public List<CreateJournalLineDto> Lines { get; set; } = new();
        }

        public class CreateJournalLineDto
        {
            public int AccountId { get; set; }
            public decimal Debit { get; set; } = 0;
            public decimal Credit { get; set; } = 0;
            public string? Description { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create(string project, [FromBody] CreateJournalDto dto)
        {
            var entry = new JournalEntry { Date = dto.Date, Description = dto.Description, EntryNumber = dto.EntryNumber };
            var lines = dto.Lines.Select(l => new JournalLine
            {
                AccountId = l.AccountId,
                Debit = l.Debit,
                Credit = l.Credit,
                Description = l.Description
            });

            // Basic balance check
            var totalDebit = dto.Lines.Sum(x => x.Debit);
            var totalCredit = dto.Lines.Sum(x => x.Credit);
            if (totalDebit != totalCredit)
                return BadRequest(new { message = "Journal not balanced. Total debit must equal total credit." });

            var id = await _service.CreateJournalEntryAsync(project, entry, lines);
            await _service.PostJournalEntryAsync(project, id); // auto post
            return CreatedAtAction(nameof(Get), new { project, id }, new { id });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string project, int id)
        {
            using var db = _factory.Create(project);
            var entry = await db.JournalEntries.FindAsync(id);
            if (entry == null) return NotFound();
            var lines = await db.JournalLines.Where(l => l.JournalEntryId == id).ToListAsync();
            return Ok(new { entry, lines });
        }

        [HttpPost("{id}/post")]
        public async Task<IActionResult> Post(string project, int id)
        {
            await _service.PostJournalEntryAsync(project, id);
            return NoContent();
        }
    }
}
