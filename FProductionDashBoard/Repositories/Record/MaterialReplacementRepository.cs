using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class MaterialReplacementRepository : Repository<MaterialReplacementRecord, MesDbContext>, IMaterialReplacementRepository
    {
        public MaterialReplacementRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task<int> AddReplacementRecordAsync(int equipmentId, int employeeId, string errorCode,
                                                                List<(int materialId, int quantity)> details,
                                                                DateTime? operatedAt = null)
        {
            await using var ctx = _factory.CreateDbContext();
            var record = new MaterialReplacementRecord
            {
                EquipmentId = equipmentId,
                EmployeeId = employeeId,
                ErrorCode = errorCode,
                CreateAt = operatedAt ?? DateTime.Now,
                ReplacementDetails = details.Select(d => new MaterialReplacementDetail
                {
                    MaterialId = d.materialId,
                    Quantity = d.quantity
                }).ToList()
            };

            ctx.MaterialReplacementRecords.Add(record);
            await ctx.SaveChangesAsync();

            return record.ReplacementId;
        }

        /// <summary>
        /// 查詢單筆更換紀錄 (含明細)
        /// </summary>
        public async Task<MaterialReplacementRecord?> GetReplacementRecordByIdAsync(int replacementId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.MaterialReplacementRecords
                .Include(r => r.Equipment)
                .Include(r => r.Employee)
                .Include(r => r.Error)
                .Include(r => r.ReplacementDetails)
                    .ThenInclude(d => d.Material)
                .FirstOrDefaultAsync(r => r.ReplacementId == replacementId);
        }

        /// <summary>
        /// 查詢所有更換紀錄 (含設備、人員)
        /// </summary>
        public async Task<List<MaterialReplacementRecord>> GetAllReplacementRecordsAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.MaterialReplacementRecords
                .Include(r => r.Equipment)
                .Include(r => r.Employee)
                .OrderByDescending(r => r.CreateAt)
                .ToListAsync();
        }

        /// <summary>
        /// 查詢某設備的更換紀錄
        /// </summary>
        public async Task<List<MaterialReplacementRecord>> GetRecordsByEquipmentAsync(int equipmentId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.MaterialReplacementRecords
                .Where(r => r.EquipmentId == equipmentId)
                .Include(r => r.ReplacementDetails)
                .ToListAsync();
        }

        /// <summary>
        /// 查詢某人員的更換紀錄
        /// </summary>
        public async Task<List<MaterialReplacementRecord>> GetRecordsByEmployeeAsync(int employeeId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.MaterialReplacementRecords
                .Where(r => r.EmployeeId == employeeId)
                .Include(r => r.ReplacementDetails)
                .ToListAsync();
        }

        /// <summary>
        /// 查詢某物料的更換紀錄 (透過明細表)
        /// </summary>
        public async Task<List<MaterialReplacementDetail>> GetRecordsByMaterialAsync(int materialId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.MaterialReplacementDetails
                .Where(d => d.MaterialId == materialId)
                .Include(d => d.ReplacementRecord)
                .ThenInclude(r => (r ?? new()).Equipment)
                .ToListAsync();
        }
    }
}
