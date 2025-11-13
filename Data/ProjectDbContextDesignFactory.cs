using Microsoft.EntityFrameworkCore.Design;

namespace WebAPI.Data
{
    public class ProjectDbContextDesignFactory : IDesignTimeDbContextFactory<ProjectDbContext>
    {
        public ProjectDbContext CreateDbContext(string[] args)
        {
            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            // Get connection string (adjust name if needed)
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // Pass a default schema name just for migrations
            const string defaultSchema = "dbo";

            return new ProjectDbContext(defaultSchema, connectionString);
        }
    }
}
