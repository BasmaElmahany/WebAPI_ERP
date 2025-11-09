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
using System.Diagnostics.Eventing.Reader;

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
   
        public async Task<IActionResult> GetAll()
        {
            try
            {
                // ✅ Extract the raw JWT token from Authorization header
                var authHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                    return Unauthorized(new { message = "Missing or invalid Authorization header." });

                var token = authHeader.Substring("Bearer ".Length).Trim();

                // ✅ Decode the token
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                // ✅ Extract user ID claim
                var userId = jwt.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Sub ||
                    c.Type == ClaimTypes.NameIdentifier ||
                    c.Type.EndsWith("nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value;

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Invalid token: missing user identifier." });

                // ✅ Fetch user from Identity
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Unauthorized(new { message = "User not found or no longer active." });

                // ✅ Extract all roles (multiple possible)
                var roles = jwt.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                // ✅ Optional debug output
                Console.WriteLine("🟢 JWT Claims:");
                foreach (var c in jwt.Claims)
                    Console.WriteLine($"   {c.Type} = {c.Value}");

                // ✅ Check if Admin
                if (roles.Contains("Admin"))
                {
                    var projects = await _context.Projects
                        .OrderBy(p => p.Name)
                        .ToListAsync();

                    return Ok(projects);
                }

                // ✅ Otherwise, return limited data or forbidden
                return Forbid("You do not have permission to access this resource.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error decoding JWT: {ex}");
                return StatusCode(500, new { message = "Error decoding or processing JWT.", error = ex.Message });
            }
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
            try
            {
                // ✅ Extract the raw JWT token from Authorization header
                var authHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                    return Unauthorized(new { message = "Missing or invalid Authorization header." });

                var token = authHeader.Substring("Bearer ".Length).Trim();

                // ✅ Decode the token
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                // ✅ Extract user ID claim
                var userId = jwt.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Sub ||
                    c.Type == ClaimTypes.NameIdentifier ||
                    c.Type.EndsWith("nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value;

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Invalid token: missing user identifier." });

                // ✅ Fetch user from Identity
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Unauthorized(new { message = "User not found or no longer active." });

                // ✅ Extract all roles (multiple possible)
                var roles = jwt.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                // ✅ Optional debug output
                Console.WriteLine("🟢 JWT Claims:");
                foreach (var c in jwt.Claims)
                    Console.WriteLine($"   {c.Type} = {c.Value}");

                // ✅ Check if Admin
                if (roles.Contains("Admin"))
                {
                    // ✅ Validate request
                    if (string.IsNullOrWhiteSpace(dto.Name))
                        return BadRequest(new { message = "Project name is required." });

                    if (await _context.Projects.AnyAsync(p => p.Name == dto.Name))
                        return BadRequest(new { message = "Project name already exists." });

                    // ✅ Create new project
                    var project = new Project
                    {
                        Name = dto.Name.Trim(),
                        Description = dto.Description
                    };

                    _context.Projects.Add(project);
                    await _context.SaveChangesAsync();

                    // ✅ Create schema
                    var param = new SqlParameter("@ProjectName", dto.Name.Trim());
                    await _context.Database.ExecuteSqlRawAsync("EXEC dbo.sp_CreateProjectFullSchema @ProjectName", param);


                    return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
                    
                }

                else
                {
                    return Ok(new { message = "Unauthorized" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in Create Project: {ex}");
                return StatusCode(500, new { message = "Error creating project.", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateProjectDto dto)
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                    return Unauthorized(new { message = "Missing or invalid Authorization header." });

                var token = authHeader.Substring("Bearer ".Length).Trim();

                // ✅ Decode the token
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                // ✅ Extract user ID claim
                var userId = jwt.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Sub ||
                    c.Type == ClaimTypes.NameIdentifier ||
                    c.Type.EndsWith("nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value;

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Invalid token: missing user identifier." });

                // ✅ Fetch user from Identity
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Unauthorized(new { message = "User not found or no longer active." });

                // ✅ Extract all roles (multiple possible)
                var roles = jwt.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                // ✅ Optional debug output
                Console.WriteLine("🟢 JWT Claims:");
                foreach (var c in jwt.Claims)
                    Console.WriteLine($"   {c.Type} = {c.Value}");

                // ✅ Check if Admin
                if (roles.Contains("Admin"))
                {

                    var project = await _context.Projects.FindAsync(id);
                    if (project == null)
                        return NotFound(new { message = "Project not found." });

                    if (!string.IsNullOrWhiteSpace(dto.Description))
                        project.Description = dto.Description.Trim();

                    if (!string.IsNullOrWhiteSpace(dto.Name) && project.Name != dto.Name)
                        project.Name = dto.Name.Trim(); // NOTE: schema rename not handled

                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Project updated successfully." });
                }
                else
                {
                    return Ok(new { message = "Unauthorized" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating project: {ex}");
                return StatusCode(500, new { message = "Error updating project.", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                    return Unauthorized(new { message = "Missing or invalid Authorization header." });

                var token = authHeader.Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                var userId = jwt.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Sub ||
                    c.Type == ClaimTypes.NameIdentifier ||
                    c.Type.EndsWith("nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value;

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Invalid token: missing user identifier." });

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Unauthorized(new { message = "User not found or inactive." });

                var roles = jwt.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                // ✅ Check if Admin
                if (!roles.Contains("Admin"))
                    return Unauthorized(new { message = "Access denied." });

                // ✅ Delete the project
                var project = await _context.Projects.FindAsync(id);
                if (project == null)
                    return NotFound(new { message = "Project not found." });

                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();

                string schemaName = project.Name; // or dynamically set from the project
               
               
                string sql = $@"
    DECLARE @schema SYSNAME = N'{schemaName}';
    DECLARE @sql NVARCHAR(MAX) = N'';

    SELECT @sql += 'DROP TABLE [' + s.name + '].[' + t.name + '];' + CHAR(13)
    FROM sys.tables AS t
    INNER JOIN sys.schemas AS s ON t.schema_id = s.schema_id
    WHERE s.name = @schema;

    EXEC sp_executesql @sql;
";
                await _context.Database.ExecuteSqlRawAsync(sql);


                await _context.Database.ExecuteSqlRawAsync(sql);

                return Ok(new { message = "Project and associated schema tables deleted successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error deleting project: {ex}");
                return StatusCode(500, new { message = "Error deleting project.", error = ex.Message });
            }
        }


    }
}
