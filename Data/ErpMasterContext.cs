using Microsoft.EntityFrameworkCore;
using WebAPI.Data.Entities;

namespace WebAPI.Data
{
    public class ErpMasterContext : DbContext
    {
        public ErpMasterContext(DbContextOptions<ErpMasterContext> options) : base(options) { }

        public DbSet<Project> Projects { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Project>(b =>
            {
                b.ToTable("Projects", schema: "dbo");
                b.HasKey(x => x.Id);
                b.Property(p => p.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            });
        }
    }
}
