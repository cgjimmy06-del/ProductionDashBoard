using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.ViewModels;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class SopChecklistSettingViewModelTests
    {
        // 註：VM 失敗路徑（exception 後呼叫 _core.Log.AddLog / AddErrorLog）會走 LogService
        // 內部的 Application.Current.Dispatcher.BeginInvoke，在 xUnit 單元測試無 WPF Dispatcher
        // 會 NRE。本測試類專注於：篩選邏輯、驗證邏輯、明細子表單行為、Happy LoadAsync/SaveAsync。
        // 失敗路徑由手動 UI 驗證涵蓋。

        private readonly Mock<IDataService> _data = new();
        private readonly Mock<IDialogService> _dialog = new();

        private SopChecklistSettingViewModel CreateVm()
        {
            var log = new LogService();
            var auth = new AuthorizationService();
            var cardReader = new Mock<ICardReaderService>().Object;
            var core = new DashboardCoreServices(log, _data.Object, auth, cardReader);
            return new SopChecklistSettingViewModel(core, _dialog.Object);
        }

        private static SopChecklist BuildSop(int sopId, string partNo, string modelName, string processName, SopType sopType, string? remark = null)
        {
            return new SopChecklist
            {
                SopId = sopId,
                Product = new Product
                {
                    PartId = sopId * 10,
                    ModelId = 100,
                    Part = new ProductPart { PartId = sopId * 10, PartNo = partNo, Name = partNo + "-name" },
                    Model = new ProductModel { ModelId = 100, Name = modelName }
                },
                Process = new WorkProcess { ProcessId = 1, Name = processName },
                SopType = sopType,
                Remark = remark
            };
        }

        // ─── LoadAsync happy path ────────────────────────────────────────────

        [Fact]
        public async Task LoadAsync_PopulatesAllCollections()
        {
            _data.Setup(d => d.GetAllProductPartsAsync()).ReturnsAsync([
                new ProductPart { PartId = 1, PartNo = "ABC11111" },
                new ProductPart { PartId = 2, PartNo = "DEF22222" }
            ]);
            _data.Setup(d => d.GetProductModelsAsync()).ReturnsAsync([new ProductModel { ModelId = 100, Name = "100A" }]);
            _data.Setup(d => d.GetWorkProcessesAsync()).ReturnsAsync([new WorkProcess { ProcessId = 1, Name = "加工" }]);
            _data.Setup(d => d.GetMaterialsByTypeAsync(1)).ReturnsAsync([new Material { MaterialId = 10, Name = "Station-1" }]);
            _data.Setup(d => d.GetMaterialsByTypeAsync(2)).ReturnsAsync([new Material { MaterialId = 20, Name = "Fixture-1" }]);
            _data.Setup(d => d.GetAllSopChecklistsAsync()).ReturnsAsync([BuildSop(1, "ABC11111", "100A", "加工", SopType.Open)]);

            var vm = CreateVm();
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.LoadCommand).ExecuteAsync(null);

            Assert.Equal(2, vm.PartList.Count);
            Assert.Single(vm.ModelList);
            Assert.Single(vm.ProcessList);
            Assert.Single(vm.StationMaterials);
            Assert.Single(vm.FixtureMaterials);
            Assert.Single(vm.SopList);
            Assert.Single(vm.FilteredSopList);
            Assert.Equal(2, vm.FilteredPartList.Count);
        }

        // ─── SOP filter ──────────────────────────────────────────────────────

        [Fact]
        public async Task SopFilter_ContainsMatchAcrossMultipleColumns()
        {
            _data.Setup(d => d.GetAllProductPartsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetProductModelsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetWorkProcessesAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetMaterialsByTypeAsync(It.IsAny<int>())).ReturnsAsync([]);
            _data.Setup(d => d.GetAllSopChecklistsAsync()).ReturnsAsync([
                BuildSop(1, "ABC11111", "100A", "加工", SopType.Open),
                BuildSop(2, "DEF22222", "200B", "組裝", SopType.Close),
                BuildSop(3, "ABC99999", "100A", "包裝", SopType.Open)
            ]);

            var vm = CreateVm();
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.LoadCommand).ExecuteAsync(null);

            vm.SopFilter = "ABC";
            Assert.Equal(2, vm.FilteredSopList.Count);

            vm.SopFilter = "200B";
            Assert.Single(vm.FilteredSopList);

            vm.SopFilter = "包裝";
            Assert.Single(vm.FilteredSopList);
        }

        [Fact]
        public async Task SopTypeFilter_FiltersByExactType()
        {
            _data.Setup(d => d.GetAllProductPartsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetProductModelsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetWorkProcessesAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetMaterialsByTypeAsync(It.IsAny<int>())).ReturnsAsync([]);
            _data.Setup(d => d.GetAllSopChecklistsAsync()).ReturnsAsync([
                BuildSop(1, "ABC11111", "100A", "加工", SopType.Open),
                BuildSop(2, "DEF22222", "200B", "組裝", SopType.Close),
                BuildSop(3, "ABC99999", "100A", "包裝", SopType.Open)
            ]);

            var vm = CreateVm();
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.LoadCommand).ExecuteAsync(null);

            vm.SopTypeFilter = SopType.Open;
            Assert.Equal(2, vm.FilteredSopList.Count);

            vm.SopTypeFilter = SopType.Close;
            Assert.Single(vm.FilteredSopList);

            vm.SopTypeFilter = null; // 全部
            Assert.Equal(3, vm.FilteredSopList.Count);
        }

        // ─── Part filter + CanCreatePart ─────────────────────────────────────

        [Fact]
        public async Task PartFilter_ContainsMatch()
        {
            _data.Setup(d => d.GetAllProductPartsAsync()).ReturnsAsync([
                new ProductPart { PartId = 1, PartNo = "ABC11111", Brand = "X", Name = "Foo" },
                new ProductPart { PartId = 2, PartNo = "ABC22222", Brand = "Y", Name = "Bar" },
                new ProductPart { PartId = 3, PartNo = "DEF33333", Brand = "Z", Name = "Baz" }
            ]);
            _data.Setup(d => d.GetProductModelsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetWorkProcessesAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetMaterialsByTypeAsync(It.IsAny<int>())).ReturnsAsync([]);
            _data.Setup(d => d.GetAllSopChecklistsAsync()).ReturnsAsync([]);

            var vm = CreateVm();
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.LoadCommand).ExecuteAsync(null);

            vm.PartFilter = "ABC";
            Assert.Equal(2, vm.FilteredPartList.Count);

            vm.PartFilter = "Bar"; // 名稱比對
            Assert.Single(vm.FilteredPartList);

            vm.PartFilter = "Z"; // 品牌比對
            Assert.Single(vm.FilteredPartList);
        }

        [Fact]
        public async Task CanCreatePart_RequiresExactly8CharsAndNoMatch()
        {
            _data.Setup(d => d.GetAllProductPartsAsync()).ReturnsAsync([
                new ProductPart { PartId = 1, PartNo = "ABC12345" }
            ]);
            _data.Setup(d => d.GetProductModelsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetWorkProcessesAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetMaterialsByTypeAsync(It.IsAny<int>())).ReturnsAsync([]);
            _data.Setup(d => d.GetAllSopChecklistsAsync()).ReturnsAsync([]);

            var vm = CreateVm();
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.LoadCommand).ExecuteAsync(null);

            vm.PartFilter = "ABC12345";
            Assert.False(vm.BeginCreatePartCommand.CanExecute(null), "已存在件號不可新建");

            vm.PartFilter = "DEF12345";
            Assert.True(vm.BeginCreatePartCommand.CanExecute(null), "8 碼且不存在可新建");

            vm.PartFilter = "DEF1234";
            Assert.False(vm.BeginCreatePartCommand.CanExecute(null), "7 碼不可新建");

            vm.PartFilter = "DEF123456";
            Assert.False(vm.BeginCreatePartCommand.CanExecute(null), "9 碼不可新建");

            vm.PartFilter = "";
            Assert.False(vm.BeginCreatePartCommand.CanExecute(null), "空字串不可新建");
        }

        [Fact]
        public async Task ConfirmCreatePart_AddsToListAndSelectsNewPart()
        {
            _data.Setup(d => d.GetAllProductPartsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetProductModelsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetWorkProcessesAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetMaterialsByTypeAsync(It.IsAny<int>())).ReturnsAsync([]);
            _data.Setup(d => d.GetAllSopChecklistsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.AddProductPartAsync(It.IsAny<ProductPartFormDto>())).ReturnsAsync(99);

            var vm = CreateVm();
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.LoadCommand).ExecuteAsync(null);

            vm.PartFilter = "NEW00001";
            vm.NewPartBrand = "BrandX";
            vm.NewPartName = "NameY";
            vm.BeginCreatePartCommand.Execute(null);
            Assert.True(vm.IsCreatingPart);

            await vm.ConfirmCreatePartCommand.ExecuteAsync(null)!;

            Assert.Single(vm.PartList);
            Assert.Equal("NEW00001", vm.PartList[0].PartNo);
            Assert.Equal(99, vm.PartList[0].PartId);
            Assert.Equal(99, vm.FormPartId);
            Assert.False(vm.IsCreatingPart);
            Assert.Equal("", vm.PartFilter);
        }

        // ─── 明細子表單 ──────────────────────────────────────────────────────

        [Fact]
        public void OpenNewItemForm_AutoIncrementsSeq()
        {
            // Strict sequential: Seq = Count + 1 regardless of existing Seq values
            var vm = CreateVm();
            vm.FormItems.Add(new SopChecklistItemFormDto { Seq = 1, CheckType = CheckType.Station });
            vm.FormItems.Add(new SopChecklistItemFormDto { Seq = 2, CheckType = CheckType.Quantity });

            vm.OpenNewItemFormCommand.Execute(null);

            Assert.True(vm.IsItemFormVisible);
            Assert.Equal(3, vm.ItemFormSeq);
            Assert.Equal(CheckType.Station, vm.ItemFormCheckType);
        }

        [Fact]
        public void OpenNewItemForm_FirstItem_SeqStartsAt1()
        {
            var vm = CreateVm();
            vm.OpenNewItemFormCommand.Execute(null);
            Assert.Equal(1, vm.ItemFormSeq);
        }

        [Fact]
        public void OnCheckTypeChange_ClearsNonRelevantFields()
        {
            var vm = CreateVm();
            vm.ItemFormCheckType = CheckType.Station;
            vm.ItemFormWorkstationNo = 3;
            vm.ItemFormQuantity = 500;
            vm.ItemFormContent = "x";

            // Switch to Fixture: clears WorkstationNo/Quantity/Content, resets Material to first FixtureMaterial (null in tests)
            vm.ItemFormCheckType = CheckType.Fixture;
            Assert.Null(vm.ItemFormWorkstationNo);
            Assert.Null(vm.ItemFormQuantity);
            Assert.Null(vm.ItemFormContent);

            // Switch to Quantity: clears Material; auto-sets Quantity to 0
            vm.ItemFormCheckType = CheckType.Quantity;
            Assert.Null(vm.ItemFormMaterialId);
            Assert.Equal(0, vm.ItemFormQuantity);

            // Switch to Other: clears all non-Other fields
            vm.ItemFormCheckType = CheckType.Other;
            Assert.Null(vm.ItemFormWorkstationNo);
            Assert.Null(vm.ItemFormMaterialId);
            Assert.Null(vm.ItemFormQuantity);
        }

        [Fact]
        public void SaveItem_Station_RequiresWorkstationAndMaterial()
        {
            var vm = CreateVm();
            vm.OpenNewItemFormCommand.Execute(null);
            vm.ItemFormCheckType = CheckType.Station;
            vm.ItemFormWorkstationNo = null;
            vm.ItemFormMaterialId = 10;

            vm.SaveItemCommand.Execute(null);
            Assert.True(vm.IsItemFormVisible, "驗證失敗時保持表單開啟");
            Assert.False(string.IsNullOrEmpty(vm.FormErrorString));
            Assert.Empty(vm.FormItems);
        }

        [Fact]
        public void SaveItem_Fixture_RequiresMaterial()
        {
            var vm = CreateVm();
            vm.OpenNewItemFormCommand.Execute(null);
            vm.ItemFormCheckType = CheckType.Fixture;
            vm.ItemFormMaterialId = null;
            vm.SaveItemCommand.Execute(null);
            Assert.False(string.IsNullOrEmpty(vm.FormErrorString));
        }

        [Fact]
        public void SaveItem_Quantity_NullQuantityDefaultsToZero()
        {
            // Quantity is no longer required; null auto-converts to 0
            var vm = CreateVm();
            vm.OpenNewItemFormCommand.Execute(null);
            vm.ItemFormCheckType = CheckType.Quantity;
            vm.ItemFormQuantity = null;
            vm.SaveItemCommand.Execute(null);
            Assert.True(string.IsNullOrEmpty(vm.FormErrorString));
            Assert.Single(vm.FormItems);
            Assert.Equal(0, vm.FormItems[0].Quantity);
        }

        [Fact]
        public void OnCheckTypeChange_ManHour_ClearsOtherFields()
        {
            var vm = CreateVm();
            vm.ItemFormWorkstationNo = 1;
            vm.ItemFormMaterialId = 10;
            vm.ItemFormContent = "some text";

            vm.ItemFormCheckType = CheckType.ManHour;

            Assert.Null(vm.ItemFormWorkstationNo);
            Assert.Null(vm.ItemFormMaterialId);
            Assert.Null(vm.ItemFormContent);
            Assert.Equal(0, vm.ItemFormQuantity);
        }

        [Fact]
        public void SaveItem_ManHour_NullQuantityDefaultsToZero()
        {
            var vm = CreateVm();
            vm.OpenNewItemFormCommand.Execute(null);
            vm.ItemFormCheckType = CheckType.ManHour;
            vm.ItemFormQuantity = null;
            vm.SaveItemCommand.Execute(null);
            Assert.True(string.IsNullOrEmpty(vm.FormErrorString));
            Assert.Single(vm.FormItems);
            Assert.Equal(0, vm.FormItems[0].Quantity);
            Assert.Equal(CheckType.ManHour, vm.FormItems[0].CheckType);
        }

        [Fact]
        public void SaveItem_Other_RequiresContent()
        {
            var vm = CreateVm();
            vm.OpenNewItemFormCommand.Execute(null);
            vm.ItemFormCheckType = CheckType.Other;
            vm.ItemFormContent = "";
            vm.SaveItemCommand.Execute(null);
            Assert.False(string.IsNullOrEmpty(vm.FormErrorString));
        }

        [Fact]
        public void SaveItem_NewItem_AddsToFormItems()
        {
            var vm = CreateVm();
            vm.OpenNewItemFormCommand.Execute(null);
            vm.ItemFormCheckType = CheckType.Quantity;
            vm.ItemFormQuantity = 500;
            vm.ItemFormRemark = "remark";

            vm.SaveItemCommand.Execute(null);

            Assert.Single(vm.FormItems);
            var item = vm.FormItems[0];
            Assert.Equal(CheckType.Quantity, item.CheckType);
            Assert.Equal(500, item.Quantity);
            Assert.Equal("remark", item.Remark);
            Assert.Null(item.MaterialId); // Quantity 不應帶 MaterialId
            Assert.False(vm.IsItemFormVisible);
        }

        [Fact]
        public void SaveItem_EditExisting_ReplacesAtIndex()
        {
            var vm = CreateVm();
            vm.FormItems.Add(new SopChecklistItemFormDto { Id = 1, Seq = 1, CheckType = CheckType.Station, WorkstationNo = 3, MaterialId = 10 });
            vm.FormItems.Add(new SopChecklistItemFormDto { Id = 2, Seq = 2, CheckType = CheckType.Quantity, Quantity = 100 });

            // 模擬 EditItem
            vm.EditItemCommand.Execute(vm.FormItems[1]);
            Assert.Equal(1, vm.EditingItemIndex);

            vm.ItemFormQuantity = 999;
            vm.SaveItemCommand.Execute(null);

            Assert.Equal(2, vm.FormItems.Count);
            Assert.Equal(999, vm.FormItems[1].Quantity);
            Assert.Equal(2, vm.FormItems[1].Id); // 保留原 Id
        }

        [Fact]
        public void RemoveItem_ConfirmYes_RemovesFromList()
        {
            _dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(true);
            var vm = CreateVm();
            var item = new SopChecklistItemFormDto { Id = 1, Seq = 1, CheckType = CheckType.Quantity, Quantity = 1 };
            vm.FormItems.Add(item);
            vm.FormItems.Add(new SopChecklistItemFormDto { Id = 2, Seq = 2, CheckType = CheckType.Quantity, Quantity = 2 });

            vm.RemoveItemCommand.Execute(item);
            Assert.Single(vm.FormItems);
            Assert.Equal(2, vm.FormItems[0].Id);
        }

        [Fact]
        public void RemoveItem_ConfirmNo_KeepsList()
        {
            _dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(false);
            var vm = CreateVm();
            var item = new SopChecklistItemFormDto { Id = 1, Seq = 1, CheckType = CheckType.Quantity, Quantity = 1 };
            vm.FormItems.Add(item);

            vm.RemoveItemCommand.Execute(item);
            Assert.Single(vm.FormItems);
        }

        // ─── SaveAsync 驗證 ──────────────────────────────────────────────────

        [Fact]
        public async Task SaveAsync_MissingPartOrModelOrProcess_SetsErrorAndDoesNotCallData()
        {
            _dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(true);
            var vm = CreateVm();
            vm.FormPartId = null; // 必填缺
            vm.FormModelId = 100;
            vm.FormProcessId = 1;
            vm.FormItems.Add(new SopChecklistItemFormDto { Seq = 1, CheckType = CheckType.Quantity, Quantity = 1 });

            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

            Assert.False(string.IsNullOrEmpty(vm.FormErrorString));
            _data.Verify(d => d.AddSopChecklistAsync(It.IsAny<SopChecklistFormDto>()), Times.Never);
        }

        [Fact]
        public async Task SaveAsync_NoItems_SetsErrorAndDoesNotCallData()
        {
            _dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(true);
            var vm = CreateVm();
            vm.FormPartId = 1;
            vm.FormModelId = 100;
            vm.FormProcessId = 1;
            // FormItems 為空

            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

            Assert.False(string.IsNullOrEmpty(vm.FormErrorString));
            _data.Verify(d => d.AddSopChecklistAsync(It.IsAny<SopChecklistFormDto>()), Times.Never);
        }

        [Fact]
        public async Task SaveAsync_NewSop_CallsAddWithDtoFromForm()
        {
            _dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(true);
            _data.Setup(d => d.GetAllProductPartsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetProductModelsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetWorkProcessesAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetMaterialsByTypeAsync(It.IsAny<int>())).ReturnsAsync([]);
            _data.Setup(d => d.GetAllSopChecklistsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.AddSopChecklistAsync(It.IsAny<SopChecklistFormDto>())).Returns(Task.CompletedTask);

            var vm = CreateVm();
            vm.FormPartId = 1;
            vm.FormModelId = 100;
            vm.FormProcessId = 1;
            vm.FormSopType = SopType.Close;
            vm.FormRemark = "test";
            vm.FormItems.Add(new SopChecklistItemFormDto { Seq = 1, CheckType = CheckType.Quantity, Quantity = 50 });

            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

            _data.Verify(d => d.AddSopChecklistAsync(It.Is<SopChecklistFormDto>(dto =>
                dto.PartId == 1 &&
                dto.ModelId == 100 &&
                dto.ProcessId == 1 &&
                dto.SopType == SopType.Close &&
                dto.Remark == "test" &&
                dto.Items.Count == 1)), Times.Once);
            _data.Verify(d => d.UpdateSopChecklistAsync(It.IsAny<SopChecklistFormDto>()), Times.Never);
        }

        [Fact]
        public async Task SaveAsync_EditingSop_CallsUpdateNotAdd()
        {
            _dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(true);
            _data.Setup(d => d.GetAllProductPartsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetProductModelsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetWorkProcessesAsync()).ReturnsAsync([]);
            _data.Setup(d => d.GetMaterialsByTypeAsync(It.IsAny<int>())).ReturnsAsync([]);
            _data.Setup(d => d.GetAllSopChecklistsAsync()).ReturnsAsync([]);
            _data.Setup(d => d.UpdateSopChecklistAsync(It.IsAny<SopChecklistFormDto>())).Returns(Task.CompletedTask);

            var vm = CreateVm();
            vm.EditingSopIdReflectionHelper(7); // 設定編輯狀態（透過反射或直接設）
            vm.FormPartId = 1;
            vm.FormModelId = 100;
            vm.FormProcessId = 1;
            vm.FormItems.Add(new SopChecklistItemFormDto { Seq = 1, CheckType = CheckType.Quantity, Quantity = 50 });

            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

            _data.Verify(d => d.UpdateSopChecklistAsync(It.Is<SopChecklistFormDto>(dto => dto.Id == 7)), Times.Once);
            _data.Verify(d => d.AddSopChecklistAsync(It.IsAny<SopChecklistFormDto>()), Times.Never);
        }
    }

    // 測試輔助：暴露 EditingSopId setter（VM 內 setter 是 private setter via [ObservableProperty]）
    internal static class SopChecklistSettingViewModelTestHelpers
    {
        public static void EditingSopIdReflectionHelper(this SopChecklistSettingViewModel vm, int id)
        {
            typeof(SopChecklistSettingViewModel)
                .GetProperty(nameof(SopChecklistSettingViewModel.EditingSopId))!
                .SetValue(vm, (int?)id);
        }
    }
}
