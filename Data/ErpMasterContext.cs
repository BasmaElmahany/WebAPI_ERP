using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data.Entities;

namespace WebAPI.Data
{
    public class ErpMasterContext : IdentityDbContext<ApplicationUser>
    {
        public ErpMasterContext(DbContextOptions<ErpMasterContext> options) : base(options) { }

        public DbSet<Project> Projects { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ✅ IMPORTANT: call base first so Identity defines its keys/tables
            base.OnModelCreating(modelBuilder);

            // ✅ Tell EF the table already exists and it should NOT manage migrations for it
            modelBuilder.Entity<Project>(b =>
            {
                b.ToTable("Projects", schema: "dbo");
                b.HasKey(x => x.Id);
                b.Property(p => p.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            });

            // ✅ Ignore migrations for Projects table
            modelBuilder.Entity<Project>().ToTable("Projects", t => t.ExcludeFromMigrations());
        }
    }
}
