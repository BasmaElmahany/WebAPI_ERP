using Microsoft.AspNetCore.Mvc;
using WebAPI.Data.Entities;
using WebAPI.Data;
using WebAPI.Services;
using Microsoft.EntityFrameworkCore;
using WebAPI.Models;
using System.Text.Json;

namespace WebAPI.Controllers
{
    [ApiController]
    [Produces("application/json")]
    [Route("api/{project}/journals")]
    public class JournalController : ControllerBase
    {
        private readonly ProjectDbContextFactory _factory;
        private readonly AccountingService _service;
        /*------------------------------------------------------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        /*--------------------------------------------Constructor ----------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        public JournalController(ProjectDbContextFactory factory, AccountingService service)
        {
            _factory = factory;
            _service = service;
        }
        /*------------------------------------------------------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        /*--------------------------------------------Create ----------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        [HttpPost]
        public async Task<IActionResult> Create(
         string project,
         [FromForm] CreateJournalDto dto,
         [FromServices] IWebHostEnvironment env)
        {
            var webRootPath = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");


            var entry = new JournalEntry
            {
                Date = dto.Date,
                Description = dto.Description,
                EntryNumber = dto.EntryNumber
            };

            var lines = dto.Lines.Select(l => new JournalLine
            {
                AccountId = l.AccountId,
                Debit = l.Debit,
                Credit = l.Credit,
                Description = l.Description
            });

            if (dto.Photo != null && dto.Photo.Length > 0)
            {
                var uploadsPath = Path.Combine(webRootPath, "images", "journals");

                if (!Directory.Exists(uploadsPath))
                    Directory.CreateDirectory(uploadsPath);

                var fileName = Guid.NewGuid() + Path.GetExtension(dto.Photo.FileName);
                var fullPath = Path.Combine(uploadsPath, fileName);

                using var stream = new FileStream(fullPath, FileMode.Create);
                await dto.Photo.CopyToAsync(stream);

                entry.PhotoUrl = $"/images/journals/{fileName}";
            }

            // Balance check
            var totalDebit = dto.Lines.Sum(x => x.Debit);
            var totalCredit = dto.Lines.Sum(x => x.Credit);

            if (totalDebit != totalCredit)
                return BadRequest(new { message = "Journal not balanced. Total debit must equal total credit." });

            var id = await _service.CreateJournalEntryAsync(project, entry, lines);
            await _service.PostJournalEntryAsync(project, id);

            return CreatedAtAction(nameof(Get), new { project, id }, new { id });
        }



        /*------------------------------------------------------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        /*--------------------------------------------Update ----------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string project, int id, [FromForm] UpdateJournalDto dto)
        {
            using var db = _factory.Create(project);

            var entry = await db.JournalEntries.FindAsync(id);
            if (entry == null)
                return NotFound();

            if (entry.Posted)
                return BadRequest(new { message = "Cannot edit a posted journal entry." });

            // Deserialize incoming line DTOs
            var dtoLines = JsonSerializer.Deserialize<List<CreateJournalLineDto>>(dto.LinesJson);

            // Convert DTO → Entity
            var lines = dtoLines.Select(l => new JournalLine
            {
                AccountId = l.AccountId,
                Debit = l.Debit,
                Credit = l.Credit,
                Description = l.Description
            }).ToList();

            // Validate balancing
            var totalDebit = lines.Sum(x => x.Debit);
            var totalCredit = lines.Sum(x => x.Credit);

            if (totalDebit != totalCredit)
                return BadRequest(new { message = "Journal not balanced. Total debit must equal total credit." });

            // Update entry fields
            entry.EntryNumber = dto.EntryNumber;
            entry.Date = dto.Date;
            entry.Description = dto.Description;

            // Handle new file upload
            if (dto.Photo != null)
            {
                string folder = Path.Combine("files", project);
                Directory.CreateDirectory(folder);

                string filename = Guid.NewGuid().ToString() + Path.GetExtension(dto.Photo.FileName);
                string path = Path.Combine(folder, filename);

                using var stream = new FileStream(path, FileMode.Create);
                await dto.Photo.CopyToAsync(stream);

                entry.PhotoUrl = $"/files/{project}/{filename}";
            }

            await _service.UpdateJournalEntryAsync(project, id, entry, lines);

            return NoContent();
        }


        /*------------------------------------------------------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        /*--------------------------------------------get by id ----------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string project, int id)
        {
            using var db = _factory.Create(project);
            var entry = await db.JournalEntries.FindAsync(id);
            if (entry == null) return NotFound();
            var lines = await db.JournalLines.Where(l => l.JournalEntryId == id).ToListAsync();
            return Ok(new { entry, lines });
        }
        /*------------------------------------------------------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        /*--------------------------------------------get all ----------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
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

            return Ok(new { list });
        }
        /*------------------------------------------------------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        /*--------------------------------------------post to ledger ----------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        [HttpPost("{id}/post")]
        public async Task<IActionResult> Post(string project, int id)
        {
            await _service.PostJournalEntryAsync(project, id);
            return NoContent();
        }
        /*------------------------------------------------------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        /*--------------------------------------------unpost from ledger ----------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
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
        /*------------------------------------------------------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        /*--------------------------------------------Delete ----------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
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
        /*------------------------------------------------------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
        /*--------------------------------------------Finish ----------------------------------------------------------------------*/
        /*------------------------------------------------------------------------------------------------------------------*/
    }
}
