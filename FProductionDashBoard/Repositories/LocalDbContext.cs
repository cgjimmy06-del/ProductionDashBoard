using FProductionDashBoard.Services.Offline;
using Microsoft.EntityFrameworkCore;

namespace FProductionDashBoard.Repositories
{
    public class LocalDbContext : DbContext
    {
        public DbSet<PendingOperation> PendingOperations { get; set; }

        public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PendingOperation>(e =>
            {
                e.HasKey(p => p.Id);
                e.Property(p => p.OperationType).HasConversion<string>();
                e.Property(p => p.Status).HasConversion<string>();
            });
        }
    }
}
