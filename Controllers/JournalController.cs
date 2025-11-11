using Microsoft.AspNetCore.Mvc;
using WebAPI.Data.Entities;
using WebAPI.Data;
using WebAPI.Services;
using Microsoft.EntityFrameworkCore;
using WebAPI.Models;

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
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string project, int id, [FromBody] CreateJournalDto dto)
        {
            using var db = _factory.Create(project);

            var entry = await db.JournalEntries.FindAsync(id);
            if (entry == null)
                return NotFound();

            if (entry.Posted)
                return BadRequest(new { message = "Cannot edit a posted journal entry." });

            // تأكد من توازن القيد
            var totalDebit = dto.Lines.Sum(x => x.Debit);
            var totalCredit = dto.Lines.Sum(x => x.Credit);
            if (totalDebit != totalCredit)
                return BadRequest(new { message = "Journal not balanced. Total debit must equal total credit." });

            // إنشاء نسخة جديدة من البيانات المحدثة
            var updatedEntry = new JournalEntry
            {
                Date = dto.Date,
                Description = dto.Description,
                EntryNumber = dto.EntryNumber
            };

            var updatedLines = dto.Lines.Select(l => new JournalLine
            {
                AccountId = l.AccountId,
                Debit = l.Debit,
                Credit = l.Credit,
                Description = l.Description
            });

            await _service.UpdateJournalEntryAsync(project, id, updatedEntry, updatedLines);
            return NoContent();
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

        [HttpGet]
        public async Task<IActionResult> GetAll(string project)
        {
            using var db = _factory.Create(project);

            var list = await db.JournalEntries
                .Select(e => new {
                    e.Id,
                    e.EntryNumber,
                    e.Date,
                    e.Description,
                    e.Posted
                })
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("{id}/post")]
        public async Task<IActionResult> Post(string project, int id)
        {
            await _service.PostJournalEntryAsync(project, id);
            return NoContent();
        }

        [HttpPost("{id}/unpost")]
        public async Task<IActionResult> Unpost(string project, int id)
        {
            try
            {
                await _service.UnpostJournalEntryAsync(project, id);
                return Ok(new { message = "Journal entry unposted successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string project, int id)
        {
            try
            {
                await _service.DeleteJournalEntryAsync(project, id);
                return Ok(new {message ="Journal Deleted Successfully"}); 
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
