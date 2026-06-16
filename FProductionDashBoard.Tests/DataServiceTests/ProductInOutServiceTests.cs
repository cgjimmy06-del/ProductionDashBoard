using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Repositories.ExtraDb;
using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace FProductionDashBoard.Tests.DataServiceTests
{
    public class ProductInOutServiceTests
    {
        private readonly Mock<IEquipmentRepository> _equipmentRep = new();
        private readonly Mock<IEmployeeRepository> _employeeRep = new();
        private readonly Mock<IMaterialRepository> _materialRep = new();
        private readonly Mock<IErrorListRepository> _errorListRep = new();
        private readonly Mock<IMaterialReplacementRepository> _materialReplacementRep = new();
        private readonly Mock<IInspectionRecordRepository> _inspectionRecordRep = new();
        private readonly Mock<ITimeSlotLookupRepository> _timeSlotLookupRep = new();
        private readonly Mock<IOfflineCacheService> _offlineCache = new();
        private readonly Mock<IRolePermissionRepository> _rolePermissionRep = new();
        private readonly Mock<IProductPartRepository> _productPartRep = new();
        private readonly Mock<IProductRepository> _productRep = new();
        private readonly Mock<ISopChecklistRepository> _sopChecklistRep = new();
        private readonly Mock<IEquipmentProductRepository> _equipmentProductRep = new();
        private readonly Mock<IOrderProductionRepository> _orderProductionRep = new();
        private readonly Mock<IProgramTuningRecordRepository> _programTuningRep = new();
        private readonly Mock<IScheduleRepository> _scheduleRep = new();
        private readonly Mock<IDbContextFactory<MesDbContext>> _mesFactory = new();
        private readonly Mock<IInfoDbRepository> _infoRep = new();
        private readonly Mock<IDataDbRepository> _dataRep = new();

        public ProductInOutServiceTests()
        {
            _materialReplacementRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _inspectionRecordRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _timeSlotLookupRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _rolePermissionRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _materialRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _productPartRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _productRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _sopChecklistRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
        }

        private DataService CreateService()
        {
            return new DataService(
                _equipmentRep.Object,
                _employeeRep.Object,
                _materialRep.Object,
                _errorListRep.Object,
                _materialReplacementRep.Object,
                _inspectionRecordRep.Object,
                _timeSlotLookupRep.Object,
                _offlineCache.Object,
                _rolePermissionRep.Object,
                _productPartRep.Object,
                _productRep.Object,
                _sopChecklistRep.Object,
                _equipmentProductRep.Object,
                _orderProductionRep.Object,
                _programTuningRep.Object,
                _scheduleRep.Object,
                _mesFactory.Object,
                _infoRep.Object,
                _dataRep.Object
            );
        }

        // ─── GetAllProductsAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetAllProductsAsync_ConnectionFail_ThrowsInvalidOperation()
        {
            _productRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            var svc = CreateService();
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetAllProductsAsync());
        }

        [Fact]
        public async Task GetAllProductsAsync_ConnectionOk_ReturnsProducts()
        {
            var products = new List<Product>
            {
                new() { ProductId = 1, PartId = 1, ModelId = 1 },
                new() { ProductId = 2, PartId = 1, ModelId = 2 }
            };
            _productRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _productRep.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(products);
            var svc = CreateService();

            var result = await svc.GetAllProductsAsync();

            Assert.Equal(2, result.Count);
        }
    }
}
