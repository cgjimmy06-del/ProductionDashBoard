using FProductionDashBoard.Repositories;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services.V1
{
    public class DataService : IDataService
    {
        public IEquipmentRepository EquipmentRep { get; }
        public IEmployeeRepository EmployeeRep { get; }
        public IMaterialRepository MaterialRep { get; }
        public IErrorListRepository ErrorListRep { get; }

        public DataService(IEquipmentRepository equipmentrep, IEmployeeRepository workerrep, IMaterialRepository materialrep, 
            IErrorListRepository errorListRep)
        {
            EquipmentRep = equipmentrep;
            EmployeeRep = workerrep;
            MaterialRep = materialrep;
            ErrorListRep = errorListRep;
        }


        // 測試用
        public async Task Demo()
        {
            var devs = await ErrorListRep.GetMessagesWithOtherAsync("zh-TW"); //zh-TW INSP0001
            //foreach (var dev in devs) { Debug.WriteLine($"{dev.LanguageCode} - {dev.Message}"); }
            //Debug.WriteLine($"{devs}");
            foreach (var dev in devs) { Debug.WriteLine($"{dev.ErrorCode} - {dev.Message}"); }


        }
        //// 插入
        //public async Task<int> AddEquipmentAsync(CreateEquipmentDto dto)
        //{
        //    var entity = new Equipment
        //    {
        //        EquipmentId = dto.EquipmentId,
        //        Name = dto.Name,
        //        Ip = dto.Ip,
        //        Port = dto.Port,
        //        Factory = dto.Factory,
        //        Building = dto.Building,
        //        Floor = dto.Floor,
        //        TypeId = dto.TypeId,
        //        DepartmentId = dto.DepartmentId,
        //        Description = dto.Description,
        //        CreateTime = DateTime.Now,
        //        UpdateTime = DateTime.Now
        //    };

        //    await _equipmentRepository.AddAsync(entity);
        //    return entity.Id; // EF Core 自動產生代理鍵
        //}

        //// 查詢
        //public async Task<List<EquipmentDto>> GetAllEquipmentsAsync()
        //{
        //    var entities = await _equipmentRepository.GetAllAsync();
        //    return entities.Select(e => new EquipmentDto
        //    {
        //        Id = e.Id,
        //        EquipmentId = e.EquipmentId,
        //        Name = e.Name,
        //        Ip = e.Ip,
        //        Port = e.Port,
        //        Factory = e.Factory,
        //        Building = e.Building,
        //        Floor = e.Floor,
        //        TypeId = e.TypeId,
        //        DepartmentId = e.DepartmentId,
        //        Description = e.Description,
        //        CreateTime = e.CreateTime,
        //        UpdateTime = e.UpdateTime
        //    }).ToList();
        //}
    }
}
