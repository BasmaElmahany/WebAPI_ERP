using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebAPI.Data.Entities;
using WebAPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using WebAPI.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebAPI.Controllers
{
    [ApiController]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly ErpMasterContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        public ProjectsController(
             ErpMasterContext context,
             RoleManager<IdentityRole> roleManager,
             UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        [HttpGet]
        [Authorize(Roles ="shusha")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _context.Projects.OrderBy(p => p.Name).ToListAsync();
            return Ok(list);
        }
        // 🧠 Get the current user's assigned project
        [HttpGet("my")]
        public async Task<IActionResult> GetMyProject()
        {
              try
               {
                   // 🔹 Get the raw token from the Authorization header
                   var authHeader = Request.Headers["Authorization"].ToString();
                   if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                       return Unauthorized(new { message = "Missing or invalid Authorization header." });

                   var token = authHeader.Substring("Bearer ".Length).Trim();

                   // 🔹 Decode the token manually
                   var handler = new JwtSecurityTokenHandler();
                   var jwt = handler.ReadJwtToken(token);

                   // 🔹 Extract the user ID from claims
                   var userId = jwt.Claims.FirstOrDefault(c =>
                       c.Type == JwtRegisteredClaimNames.Sub ||
                       c.Type == ClaimTypes.NameIdentifier ||
                       c.Type.Contains("nameidentifier"))?.Value;

                   if (string.IsNullOrEmpty(userId))
                       return Unauthorized(new { message = "Invalid token: missing user identifier." });

                   // 🔹 Get the user by ID
                   var user = await _userManager.FindByIdAsync(userId);
                   if (user == null)
                       return Unauthorized(new { message = "User not found or not logged in." });

                   if (user.ProjectId == null)
                       return NotFound(new { message = "User is not assigned to any project." });

                   // 🔹 Retrieve the project
                   var project = await _context.Projects.FindAsync(user.ProjectId);
                   if (project == null)
                       return NotFound(new { message = "Assigned project not found." });

                   // ✅ Optional: Log decoded claims for debugging
                   Console.WriteLine("🟢 Decoded JWT Claims:");
                   foreach (var claim in jwt.Claims)
                       Console.WriteLine($"   {claim.Type} = {claim.Value}");

                   // ✅ Return the project info
                   return Ok(new
                   {
                       project,
                       decodedUser = new
                       {
                           Id = user.Id,
                           user.FullName,
                           user.Email,
                           user.ProjectId
                       }
                   });
               }
               catch (Exception ex)
               {
                   Console.WriteLine($"❌ Error decoding JWT: {ex.Message}");
                   return StatusCode(500, new { message = "Error decoding token.", error = ex.Message });
               }
        
        }
        



        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var p = await _context.Projects.FindAsync(id);
            if (p == null) return NotFound();
            return Ok(p);
        }


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Project name required" });

            if (await _context.Projects.AnyAsync(p => p.Name == dto.Name))
                return BadRequest(new { message = "Project name already exists" });

            var project = new Project
            {
                Name = dto.Name.Trim(),
                Description = dto.Description
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // 🧱 إنشاء قاعدة المشروع (schema)
            var param = new SqlParameter("@ProjectName", dto.Name.Trim());
            await _context.Database.ExecuteSqlRawAsync("EXEC dbo.sp_CreateProjectFullSchema @ProjectName", param);

            // 🔐 إنشاء Role بنفس اسم المشروع
            if (!await _roleManager.RoleExistsAsync(dto.Name.Trim()))
            {
                var role = new IdentityRole(dto.Name.Trim());
                var roleResult = await _roleManager.CreateAsync(role);

                if (!roleResult.Succeeded)
                {
                    return StatusCode(500, new
                    {
                        message = "Project created, but failed to create role.",
                        errors = roleResult.Errors
                    });
                }
            }

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
