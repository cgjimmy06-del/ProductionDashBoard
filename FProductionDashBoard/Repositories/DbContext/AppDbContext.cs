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
using FProductionDashBoard.Models.Extra;

namespace FProductionDashBoard.Repositories
{
    public class MesDbContext : DbContext
    {
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<ErrorList> ErrorLists { get; set; }
        public DbSet<ErrorTranslation> ErrorTranslations { get; set; }
        public DbSet<MaterialReplacementRecord> MaterialReplacementRecords { get; set; }
        public DbSet<MaterialReplacementDetail> MaterialReplacementDetails { get; set; }
        public DbSet<InspectionRecord> InspectionRecords { get; set; }
        public DbSet<TimeSlotLookup> TimeSlotLookups { get; set; }
        public DbSet<ProductPart> ProductParts { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<SopChecklist> SopChecklists { get; set; }
        public DbSet<SopChecklistItem> SopChecklistItems { get; set; }
        public DbSet<EquipmentProduct> EquipmentProducts { get; set; }
        public DbSet<OrderProduction> OrderProductions { get; set; }
        public DbSet<ProgramTuningRecord> ProgramTuningRecords { get; set; }

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

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
            // 不使用 ApplyConfigurationsFromAssembly，避免污染 MesDbContext 的 model
            // 新增 Dapper View 對應的 EF Configuration 時，在此明確 ApplyConfiguration
            base.OnModelCreating(modelBuilder);
        }
    }

    public class InfoDbContext : DbContext
    {
        public DbSet<MesCustomerCode> MesCustomerCodes { get; set; }
        public DbSet<MesDevice> MesDevices { get; set; }

        public InfoDbContext(DbContextOptions<InfoDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 不使用 ApplyConfigurationsFromAssembly，避免污染 MesDbContext/DataDbContext 的 model
            modelBuilder.ApplyConfiguration(new MesCustomerCodeConfiguration());
            modelBuilder.ApplyConfiguration(new MesDeviceConfiguration());
            base.OnModelCreating(modelBuilder);
        }
    }
}
