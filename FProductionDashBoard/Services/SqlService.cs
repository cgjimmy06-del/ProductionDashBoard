using FProductionDashBoard.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace FProductionDashBoard
{
    public interface IDataService
    {
    }

    public class SqlService : IDataService
    {
        public IEquipmentRepository EquipmentRep { get; }
        public IEmployeeRepository EmployeeRep { get; }

        public SqlService(IEquipmentRepository equipmentrep, IEmployeeRepository workerrep)
        {
            EquipmentRep = equipmentrep;
            EmployeeRep = workerrep;

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
