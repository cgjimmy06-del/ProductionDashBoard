using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory; // 主要應用於 EF Core 應用程式的啟動配置或需要深入診斷 SQL 執行時的場景
using FProductionDashBoard.Models;

namespace FProductionDashBoard.Repositories
{
    public class MesDbContext : DbContext
    {
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<Employee> Employees { get; set; }

        public MesDbContext(DbContextOptions<MesDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(MesDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
    public class DataDbContext : DbContext
    {
        public DataDbContext(DbContextOptions<DataDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(DataDbContext).Assembly);
            //modelBuilder.ApplyConfiguration(new DeviceInfoConfiguration());
            base.OnModelCreating(modelBuilder);
        }
    }
}
