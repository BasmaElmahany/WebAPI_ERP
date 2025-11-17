using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebAPI.Data.Entities;
using WebAPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using WebAPI.Models;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace WebAPI.Controllers
{
    [ApiController]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly ErpMasterContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProjectsController(
            ErpMasterContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ---------------------------------------------------------
        // INTERNAL UTILITIES
        // ---------------------------------------------------------
        private JwtSecurityToken? DecodeToken(out string? userId, out List<string> roles)
        {
            userId = null;
            roles = new List<string>();

            try
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                    return null;

                var token = authHeader.Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                userId = jwt.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Sub ||
                    c.Type == ClaimTypes.NameIdentifier ||
                    c.Type.Contains("nameidentifier"))?.Value;

                roles = jwt.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                return jwt;
            }
            catch
            {
                return null;
            }
        }

        private IActionResult Fail(string msg)
        {
            return Ok(new { success = false, message = msg });
        }

        private IActionResult Success(object? data = null, string? msg = null)
        {
            return Ok(new { success = true, data, message = msg });
        }

        private async Task<ApplicationUser?> GetUser()
        {
            DecodeToken(out string? userId, out _);
            if (userId == null) return null;
            return await _userManager.FindByIdAsync(userId);
        }

        private bool IsAdmin(List<string> roles)
        {
            return roles.Contains("Admin");
        }

        // ---------------------------------------------------------
        // GET ALL PROJECTS
        // ---------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            DecodeToken(out string? userId, out List<string> roles);
            if (userId == null) return Fail("Invalid or missing token.");

            if (!IsAdmin(roles))
                return Fail("Forbidden: You must be an Admin.");

            var projects = await _context.Projects
                .OrderBy(p => p.Name)
                .ToListAsync();

            return Success(projects);
        }

        // ---------------------------------------------------------
        // GET MY PROJECT
        // ---------------------------------------------------------
        [HttpGet("my")]
        public async Task<IActionResult> GetMyProject()
        {
            DecodeToken(out string? userId, out _);
            if (userId == null) return Fail("Invalid or missing token.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Fail("User not found.");
            if (user.ProjectId == null) return Fail("User not assigned to any project.");

            var project = await _context.Projects.FindAsync(user.ProjectId);
            if (project == null) return Fail("Assigned project not found.");

            return Success(new
            {
                project,
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.ProjectId
                }
            });
        }

        // ---------------------------------------------------------
        // GET BY ID
        // ---------------------------------------------------------
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) return Fail("Project not found.");

            return Success(project);
        }

        // ---------------------------------------------------------
        // CREATE PROJECT
        // ---------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
        {
            DecodeToken(out string? userId, out List<string> roles);
            if (userId == null) return Fail("Invalid or missing token.");
            if (!IsAdmin(roles)) return Fail("Forbidden: Only Admins can create projects.");

            if (string.IsNullOrWhiteSpace(dto.Name))
                return Fail("Project name is required.");

            if (await _context.Projects.AnyAsync(p => p.Name == dto.Name))
                return Fail("Project name already exists.");

            var project = new Project
            {
                Name = dto.Name.Trim(),
                Description = dto.Description
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            var param = new SqlParameter("@ProjectName", dto.Name.Trim());
            await _context.Database.ExecuteSqlRawAsync("EXEC dbo.sp_CreateProjectFullSchema @ProjectName", param);

            return Success(project, "Project created successfully.");
        }

        // ---------------------------------------------------------
        // UPDATE
        // ---------------------------------------------------------
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateProjectDto dto)
        {
            DecodeToken(out string? userId, out List<string> roles);
            if (userId == null) return Fail("Invalid or missing token.");
            if (!IsAdmin(roles)) return Fail("Forbidden: Only Admins can update projects.");

            var project = await _context.Projects.FindAsync(id);
            if (project == null) return Fail("Project not found.");

            if (!string.IsNullOrWhiteSpace(dto.Description))
                project.Description = dto.Description;

            if (!string.IsNullOrWhiteSpace(dto.Name) && dto.Name != project.Name)
            {
                var oldSchema = project.Name;
                var newSchema = dto.Name.Trim();

                await _context.Database.BeginTransactionAsync();
                try
                {
                    await _context.Database.ExecuteSqlRawAsync($"CREATE SCHEMA [{newSchema}]");

                    string moveSql = $@"
                        DECLARE @sql NVARCHAR(MAX) = N'';
                        SELECT @sql += 'ALTER SCHEMA [{newSchema}] TRANSFER [{oldSchema}].[' + t.name + '];'
                        FROM sys.tables t
                        JOIN sys.schemas s ON t.schema_id = s.schema_id
                        WHERE s.name = '{oldSchema}';
                        EXEC (@sql);
                    ";

                    await _context.Database.ExecuteSqlRawAsync(moveSql);
                    await _context.Database.ExecuteSqlRawAsync($"DROP SCHEMA [{oldSchema}]");

                    project.Name = newSchema;

                    await _context.SaveChangesAsync();
                    await _context.Database.CommitTransactionAsync();

                    return Success(project, "Project updated & schema renamed.");
                }
                catch (Exception ex)
                {
                    await _context.Database.RollbackTransactionAsync();
                    return Fail("Error renaming schema: " + ex.Message);
                }
            }

            await _context.SaveChangesAsync();
            return Success(project, "Project updated successfully.");
        }

        // ---------------------------------------------------------
        // DELETE
        // ---------------------------------------------------------
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            DecodeToken(out string? userId, out List<string> roles);
            if (userId == null) return Fail("Invalid or missing token.");
            if (!IsAdmin(roles)) return Fail("Forbidden: Only Admins can delete projects.");

            var project = await _context.Projects.FindAsync(id);
            if (project == null) return Fail("Project not found.");

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            string schema = project.Name;

            string sql = $@"
                DECLARE @sql NVARCHAR(MAX)='';
                SELECT @sql += 'DROP TABLE [' + s.name + '].[' + t.name + '];'
                FROM sys.tables t
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name='{schema}';
                EXEC(@sql);
            ";

            await _context.Database.ExecuteSqlRawAsync(sql);

            return Success(null, "Project & schema deleted successfully.");
        }
    }
}
