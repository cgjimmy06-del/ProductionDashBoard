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
        public IMaterialReplacementRepository MaterialReplacementRep { get; }

        public DataService(IEquipmentRepository equipmentrep, IEmployeeRepository workerrep, IMaterialRepository materialrep,
            IErrorListRepository errorListRep, IMaterialReplacementRepository materialReplacementRep)
        {
            EquipmentRep = equipmentrep;
            EmployeeRep = workerrep;
            MaterialRep = materialrep;
            ErrorListRep = errorListRep;
            MaterialReplacementRep = materialReplacementRep;
        }







        // 測試用
        public async Task Demo()
        {
            // 查詢
            var devs = await ErrorListRep.GetMessagesWithOtherAsync("zh-TW"); //zh-TW INSP0001
            //foreach (var dev in devs) { Debug.WriteLine($"{dev.LanguageCode} - {dev.Message}"); }
            //Debug.WriteLine($"{devs}");
            foreach (var (ErrorCode, Message, Category) in devs) { Debug.WriteLine($"{ErrorCode} - {Message}"); }

            // 插入
            //var replacementId = await MaterialReplacementRep.AddReplacementRecordAsync(
            //                    equipmentId: 3,
            //                    employeeId: 2,
            //                    errorCode: "MTRP0001",
            //                    details: new List<(int materialId, int quantity)>
            //                    {
            //                        (materialId: 1, quantity: 1),
            //                        (materialId: 3, quantity: 1),
            //                        (materialId: 4, quantity: 3),
            //                    }
            //                );
            //Debug.WriteLine($"新增成功，ReplacementId = {replacementId}");
            // 更新


            // 刪除


        }
    }
}
