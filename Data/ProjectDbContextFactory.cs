namespace WebAPI.Data
{
    public class ProjectDbContextFactory
    {
        private readonly string _connectionString;
        public ProjectDbContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public ProjectDbContext Create(string projectSchemaName)
        {
            return new ProjectDbContext(projectSchemaName, _connectionString);
        }
    }
}
