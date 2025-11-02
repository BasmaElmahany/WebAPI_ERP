using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebAPI.Data.Entities;
using WebAPI.Data;
using Microsoft.EntityFrameworkCore;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebAPI.Controllers
{
    [ApiController]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly ErpMasterContext _context;

        public ProjectsController(ErpMasterContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _context.Projects.OrderBy(p => p.Name).ToListAsync();
            return Ok(list);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var p = await _context.Projects.FindAsync(id);
            if (p == null) return NotFound();
            return Ok(p);
        }

        public class CreateProjectDto { public string Name { get; set; } = null!; public string? Description { get; set; } }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Name required" });

            if (await _context.Projects.AnyAsync(p => p.Name == dto.Name))
                return BadRequest(new { message = "Project name exists" });

            var project = new Project { Name = dto.Name.Trim(), Description = dto.Description };
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // Call stored procedure to create schema/tables
            var param = new SqlParameter("@ProjectName", dto.Name.Trim());
            await _context.Database.ExecuteSqlRawAsync("EXEC dbo.sp_CreateProjectFullSchema @ProjectName", param);

            return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateProjectDto dto)
        {
            var p = await _context.Projects.FindAsync(id);
            if (p == null) return NotFound();
            p.Description = dto.Description;
            if (!string.IsNullOrWhiteSpace(dto.Name) && p.Name != dto.Name)
            {
                // renaming schema is not handled here — keep simple
                p.Name = dto.Name.Trim();
            }
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _context.Projects.FindAsync(id);
            if (p == null) return NotFound();

            // Note: this does NOT drop schema/tables. You can extend to run DROP SCHEMA if needed.
            _context.Projects.Remove(p);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
