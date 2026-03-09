using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace FProductionDashBoard.Repositories
{
    public class AppDbContext : DbContext
    {
        public DbSet<DeviceInfo> Devices { get; set; }
        public DbSet<WorkerInfo> Workers { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            modelBuilder.ApplyConfiguration(new DeviceInfoConfiguration());

            // 設定關聯：Worker (1) ↔ Devices (多)
            //modelBuilder.Entity<DeviceInfo>()
            //    .HasOne(d => d.Worker)
            //    .WithMany(w => w.Devices)
            //    .HasForeignKey(d => d.WorkerID);

            base.OnModelCreating(modelBuilder);
        }
    }
}
