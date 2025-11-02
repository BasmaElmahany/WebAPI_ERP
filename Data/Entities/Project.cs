using System.ComponentModel.DataAnnotations;

namespace WebAPI.Data.Entities
{
    public class Project
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string? Description { get; set; }
    }
}
