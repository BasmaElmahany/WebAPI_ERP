using Microsoft.EntityFrameworkCore;
using WebAPI.Data.Entities;

namespace WebAPI.Data
{
    public class ProjectDbContext : DbContext
    {
        private readonly string _schema;
        private readonly string _connectionString;

        public ProjectDbContext(string schema, string connectionString)
        {
            _schema = schema;
            _connectionString = connectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
                optionsBuilder.UseSqlServer(_connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(_schema);

            modelBuilder.Entity<ChartOfAccount>().ToTable("ChartOfAccounts").HasKey(x => x.Id);
            modelBuilder.Entity<FixedAsset>().ToTable("FixedAssets").HasKey(x => x.Id);
            modelBuilder.Entity<Account>().ToTable("Accounts").HasKey(x => x.Id);
            modelBuilder.Entity<JournalEntry>().ToTable("JournalEntries").HasKey(x => x.Id);
            modelBuilder.Entity<JournalLine>().ToTable("JournalLines").HasKey(x => x.Id);
            modelBuilder.Entity<LedgerEntry>().ToTable("LedgerEntries").HasKey(x => x.Id);
            modelBuilder.Entity<Revenue>().ToTable("Revenues").HasKey(x => x.Id);
            modelBuilder.Entity<Expense>().ToTable("Expenses").HasKey(x => x.Id);

            base.OnModelCreating(modelBuilder);
        }

        public DbSet<ChartOfAccount> ChartOfAccounts { get; set; } = null!;
        public DbSet<FixedAsset> FixedAssets { get; set; } = null!;
        public DbSet<Account> Accounts { get; set; } = null!;
        public DbSet<JournalEntry> JournalEntries { get; set; } = null!;
        public DbSet<JournalLine> JournalLines { get; set; } = null!;
        public DbSet<LedgerEntry> LedgerEntries { get; set; } = null!;
        public DbSet<Revenue> Revenues { get; set; } = null!;
        public DbSet<Expense> Expenses { get; set; } = null!;
    }
}
