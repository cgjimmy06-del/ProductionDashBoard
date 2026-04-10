using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

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
