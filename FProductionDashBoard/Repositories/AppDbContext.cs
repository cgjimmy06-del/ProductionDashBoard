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
    public class MesDbContext : DbContext
    {
        public DbSet<DeviceInfo> Devices { get; set; }
        public DbSet<UserInfo> Workers { get; set; }

        public MesDbContext(DbContextOptions<MesDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(MesDbContext).Assembly);
            //modelBuilder.ApplyConfiguration(new DeviceInfoConfiguration());

            base.OnModelCreating(modelBuilder);
        }
    }
    public class DataDbContext : DbContext
    {


        public DataDbContext(DbContextOptions<DataDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(DataDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
