using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebAPI.Data.Entities;
using WebAPI.Models;
using WebAPI.Settings;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly JwtSettings _jwtSettings;
        private readonly RoleManager<IdentityRole> _roleManager;
        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IOptions<JwtSettings> jwtSettings, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtSettings = jwtSettings.Value;

            _roleManager = roleManager;

        }

        // POST: api/auth/register
     

      
            [HttpPost("register")]
            public async Task<IActionResult> Register([FromBody] RegisterDto dto)
            {
                var existingUser = await _userManager.FindByEmailAsync(dto.Email);
                if (existingUser != null)
                    return BadRequest(new { message = "User already exists" });

                var user = new ApplicationUser
                {
                    FullName = dto.FullName,
                    Email = dto.Email,
                    UserName = dto.Email,
                    ProjectId = dto.ProjectId
                };

                var result = await _userManager.CreateAsync(user, dto.Password);
                if (!result.Succeeded)
                    return BadRequest(result.Errors);

                // ✅ تأكد أن الدور proj5 موجود
                if (!await _roleManager.RoleExistsAsync("proj5"))
                    await _roleManager.CreateAsync(new IdentityRole("proj5"));

                // ✅ أضف المستخدم إلى الدور proj5
                await _userManager.AddToRoleAsync(user, "proj5");

                return Ok(new { message = "Registration successful and added to proj5 role" });
            }

        

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
                throw new Exception("Invalid credentials");

            try
            {
                var token = await GenerateJwtToken(user);
                return Ok(new { token });
            }
            catch (Exception ex) {
                return Unauthorized(new { message = "Invalid Email or password "});
            }

          
           
        }
        [Authorize(Roles = "proj5")]
        [HttpGet("admin/dashboard")]
        public IActionResult GetAdminDashboard()
        {
            return Ok("Welcome proj5!");
        }


        private async Task<string> GenerateJwtToken(ApplicationUser user)
        {
            var claims = await GetUserClaimsAsync(user);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<List<Claim>> GetUserClaimsAsync(ApplicationUser user)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Name, user.FullName ?? user.UserName),
        new Claim(ClaimTypes.Email, user.Email)
    };

            // 🔹 Get roles from ASP.NET Identity
            var roles = await _userManager.GetRolesAsync(user);

            // 🔹 Add each role as a claim
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            return claims;
        }



    }
}
