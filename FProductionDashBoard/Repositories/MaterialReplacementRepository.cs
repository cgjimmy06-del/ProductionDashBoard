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
        public MaterialReplacementRepository(MesDbContext context) : base(context)
        {
        }

        public async Task<int> AddReplacementRecordAsync(int equipmentId, int employeeId, string errorCode,
                                                                List<(int materialId, int quantity)> details,
                                                                DateTime? operatedAt = null)
        {
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

            _context.MaterialReplacementRecords.Add(record);
            await _context.SaveChangesAsync();

            return record.ReplacementId; // 回傳主表 PK
        }

        // 查詢
        /// <summary>
        /// 查詢單筆更換紀錄 (含明細)
        /// </summary>
        public async Task<MaterialReplacementRecord?> GetReplacementRecordByIdAsync(int replacementId)
        {
            return await _context.MaterialReplacementRecords
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
            return await _context.MaterialReplacementRecords
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
            return await _context.MaterialReplacementRecords
                .Where(r => r.EquipmentId == equipmentId)
                .Include(r => r.ReplacementDetails)
                .ToListAsync();

            //public class ReplacementRecordDto
            //{
            //    public int ReplacementId { get; set; }
            //    public string EquipmentCode { get; set; } = string.Empty;
            //    public string EmployeeName { get; set; } = string.Empty;
            //    public string ErrorCode { get; set; } = string.Empty;
            //    public DateTime CreateAt { get; set; }
            //    public List<ReplacementDetailDto> Details { get; set; } = new();
            //}
            //public class ReplacementDetailDto
            //{
            //    public string MaterialCode { get; set; } = string.Empty;
            //    public string MaterialName { get; set; } = string.Empty;
            //    public int Quantity { get; set; }
            //}
            //var records = await _context.MaterialReplacementRecords
            //                    .Where(r => r.EquipmentId == equipmentId)
            //                    .Include(r => r.Equipment)
            //                    .Include(r => r.Employee)
            //                    .Include(r => r.ReplacementDetails)
            //                        .ThenInclude(d => d.Material)
            //                    .ToListAsync();
            //// 轉換成 DTO，顯示用
            //return records.Select(r => new ReplacementRecordDto
            //{
            //    ReplacementId = r.ReplacementId,
            //    EquipmentCode = r.Equipment?.DeviceCode ?? string.Empty,
            //    EmployeeName = r.Employee?.Name ?? string.Empty,
            //    ErrorCode = r.ErrorCode,
            //    CreateAt = r.CreateAt,
            //    Details = r.ReplacementDetails.Select(d => new ReplacementDetailDto
            //    {
            //        MaterialCode = d.Material?.MaterialCode ?? string.Empty,
            //        MaterialName = d.Material?.Name ?? string.Empty,
            //        Quantity = d.Quantity
            //    }).ToList()
            //}).ToList();
        }

        /// <summary>
        /// 查詢某人員的更換紀錄
        /// </summary>
        public async Task<List<MaterialReplacementRecord>> GetRecordsByEmployeeAsync(int employeeId)
        {
            return await _context.MaterialReplacementRecords
                .Where(r => r.EmployeeId == employeeId)
                .Include(r => r.ReplacementDetails)
                .ToListAsync();
        }

        /// <summary>
        /// 查詢某物料的更換紀錄 (透過明細表)
        /// </summary>
        public async Task<List<MaterialReplacementDetail>> GetRecordsByMaterialAsync(int materialId)
        {
            return await _context.MaterialReplacementDetails
                .Where(d => d.MaterialId == materialId)
                .Include(d => d.ReplacementRecord)
                .ThenInclude(r => (r ?? new()).Equipment)
                .ToListAsync();
        }
    }
}
