using Microsoft.AspNetCore.Identity;

namespace WebAPI.Data.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        // Example: track which project the user belongs to
        public int? ProjectId { get; set; }
    }
}
