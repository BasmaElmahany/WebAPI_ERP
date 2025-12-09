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

        public JournalController(ProjectDbContextFactory factory, AccountingService service)
        {
            _factory = factory;
            _service = service;
        }
        [HttpPost]
        public async Task<IActionResult> Create(
     string project,
     [FromForm] CreateJournalDto dto,
     [FromServices] IWebHostEnvironment env)
        {
            Console.WriteLine("RAW>> " + dto.LinesJson);
            var webRootPath = env.WebRootPath
                ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            // -------------------------------
            // Create project folder dynamically
            // -------------------------------
            var safeProjectName = project.Trim();
            var projectFolder = Path.Combine(webRootPath, "files", safeProjectName);

            if (!Directory.Exists(projectFolder))
                Directory.CreateDirectory(projectFolder);

            // -------------------------------
            // Parse LinesJson
            // -------------------------------
            var rawJson = dto.LinesJson.Trim();

            if (rawJson.StartsWith("\"") && rawJson.EndsWith("\""))
            {
                rawJson = rawJson.Substring(1, rawJson.Length - 2);
                rawJson = rawJson.Replace("\\\"", "\"");
            }

            var lineDtos = JsonSerializer.Deserialize<List<CreateJournalLineDto>>(rawJson);

            // -------------------------------
            // Create Entry
            // -------------------------------
            var entry = new JournalEntry
            {
                Date = dto.Date,
                Description = dto.Description,
                EntryNumber = dto.EntryNumber
            };

            var lines = lineDtos.Select(l => new JournalLine
            {
                AccountId = l.accountId,
                Debit = l.debit,
                Credit = l.credit,
                Description = l.description
            });

            // -------------------------------
            // File Upload (PDF or IMAGE)
            // -------------------------------
            if (dto.Photo != null && dto.Photo.Length > 0)
            {
                // Allow ONLY image or pdf
                var allowed = new[] { ".png", ".jpg", ".jpeg", ".pdf" };
                var ext = Path.GetExtension(dto.Photo.FileName).ToLower();

                if (!allowed.Contains(ext))
                    return BadRequest(new { message = "Only PNG, JPG, JPEG, or PDF files are allowed." });

                var fileName = Guid.NewGuid() + ext;
                var fullPath = Path.Combine(projectFolder, fileName);

                using var stream = new FileStream(fullPath, FileMode.Create);
                await dto.Photo.CopyToAsync(stream);

                // store relative URL
                entry.PhotoUrl = $"/files/{safeProjectName}/{fileName}";
            }

            // -------------------------------
            // Balanced Check
            // -------------------------------
            var totalDebit = lineDtos.Sum(x => x.debit);
            var totalCredit = lineDtos.Sum(x => x.credit);

            if (totalDebit != totalCredit)
                return BadRequest(new { message = "Journal not balanced. Total debit must equal total credit." });

            // -------------------------------
            // Save Entry + Lines
            // -------------------------------
            var id = await _service.CreateJournalEntryAsync(project, entry, lines);

            await _service.PostJournalEntryAsync(project, id);

            return CreatedAtAction(nameof(Get), new { project, id }, new { id });
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
           string project,
           int id,
           [FromForm] CreateJournalDto dto,
           [FromServices] IWebHostEnvironment env)
        {
            using var db = _factory.Create(project);

            var entry = await db.JournalEntries.FindAsync(id);
            if (entry == null)
                return NotFound();

            if (entry.Posted)
                return BadRequest(new { message = "Cannot edit a posted journal entry." });

            // ---------------------------------------------
            // Create dynamic project folder /files/{project}
            // ---------------------------------------------
            var webRootPath = env.WebRootPath
                ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            var safeProjectName = project.Trim();
            var projectFolder = Path.Combine(webRootPath, "files", safeProjectName);

            if (!Directory.Exists(projectFolder))
                Directory.CreateDirectory(projectFolder);

            // ---------------------------------------------
            // Parse LinesJson
            // ---------------------------------------------
            if (string.IsNullOrWhiteSpace(dto.LinesJson))
                return BadRequest(new { message = "LinesJson is required." });

            var rawJson = dto.LinesJson.Trim();

            if (rawJson.StartsWith("\"") && rawJson.EndsWith("\""))
            {
                rawJson = rawJson.Substring(1, rawJson.Length - 2);
                rawJson = rawJson.Replace("\\\"", "\"");
            }

            List<CreateJournalLineDto> lineDtos;
            try
            {
                lineDtos = JsonSerializer.Deserialize<List<CreateJournalLineDto>>(rawJson);
            }
            catch
            {
                return BadRequest(new { message = "Invalid LinesJson format." });
            }

            // ---------------------------------------------
            // Validate balance
            // ---------------------------------------------
            var totalDebit = lineDtos.Sum(x => x.debit);
            var totalCredit = lineDtos.Sum(x => x.credit);

            if (totalDebit != totalCredit)
                return BadRequest(new { message = "Journal not balanced." });

            // ---------------------------------------------
            // Update entry fields
            // ---------------------------------------------
            entry.Date = dto.Date;
            entry.Description = dto.Description;
            entry.EntryNumber = dto.EntryNumber;

            // ---------------------------------------------
            // File Upload (PDF or IMAGE)
            // ---------------------------------------------
            if (dto.Photo != null && dto.Photo.Length > 0)
            {
                var allowed = new[] { ".png", ".jpg", ".jpeg", ".pdf" };
                var ext = Path.GetExtension(dto.Photo.FileName).ToLower();

                if (!allowed.Contains(ext))
                    return BadRequest(new { message = "Only PNG, JPG, JPEG, or PDF files are allowed." });

                // Delete old file if exists
                if (!string.IsNullOrWhiteSpace(entry.PhotoUrl))
                {
                    var oldFile = Path.Combine(webRootPath, entry.PhotoUrl.TrimStart('/').Replace("/", "\\"));
                    if (System.IO.File.Exists(oldFile))
                        System.IO.File.Delete(oldFile);
                }

                // Save new file
                var fileName = Guid.NewGuid() + ext;
                var fullPath = Path.Combine(projectFolder, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                    await dto.Photo.CopyToAsync(stream);

                entry.PhotoUrl = $"/files/{safeProjectName}/{fileName}";
            }

            // ---------------------------------------------
            // Replace existing journal lines
            // ---------------------------------------------
            var existingLines = db.JournalLines.Where(l => l.JournalEntryId == id);
            db.JournalLines.RemoveRange(existingLines);

            var newLines = lineDtos.Select(l => new JournalLine
            {
                JournalEntryId = id,
                AccountId = l.accountId,
                Debit = l.debit,
                Credit = l.credit,
                Description = l.description
            });

            await db.JournalLines.AddRangeAsync(newLines);

            await db.SaveChangesAsync();

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
                    e.PhotoUrl,
                    e.Posted
                })
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            return Ok(new { list });
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
