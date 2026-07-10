using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    public class JsonChartDefinitionStoreTests
    {
        /// <summary>記憶體後端：以委派注入取代 JsonDataService，測試不落地實體檔案</summary>
        private sealed class MemoryBacking
        {
            public List<ChartDefinition> Data = new();
            public int SaveCount;
        }

        private static JsonChartDefinitionStore BuildStore(MemoryBacking backing)
            => new(
                _ => new List<ChartDefinition>(backing.Data),
                (list, _) => { backing.Data = new List<ChartDefinition>(list); backing.SaveCount++; });

        private static ChartDefinition NewChart(string id, int sortOrder = 99, bool isDefault = false)
            => new() { Id = id, Name = id, SortOrder = sortOrder, IsDefault = isDefault };

        // --- Load / 種子 ---

        [Fact]
        public void Load_WhenEmpty_SeedsDefaultsAndPersists()
        {
            var backing = new MemoryBacking();
            var store = BuildStore(backing);

            var result = store.Load();

            Assert.Equal(2, result.Count);
            Assert.Equal(DefaultChartDefinitions.EquipmentOverviewId, result[0].Id);
            Assert.True(result[0].IsDefault);
            Assert.Equal(DefaultChartDefinitions.ScheduleBoardId, result[1].Id);
            Assert.False(result[1].IsDefault);
            Assert.True(backing.SaveCount >= 1);
        }

        [Fact]
        public void Load_WhenDefaultMissing_ReinsertsLockedDefault()
        {
            var backing = new MemoryBacking { Data = { NewChart("custom-1", sortOrder: 5) } };
            var store = BuildStore(backing);

            var result = store.Load();

            Assert.Contains(result, d => d.Id == DefaultChartDefinitions.EquipmentOverviewId && d.IsDefault);
            Assert.Contains(result, d => d.Id == "custom-1");
            // 排程看板為一般圖表，使用者可自行刪除，不重種
            Assert.DoesNotContain(result, d => d.Id == DefaultChartDefinitions.ScheduleBoardId);
        }

        [Fact]
        public void Load_ReturnsOrderedBySortOrder()
        {
            var backing = new MemoryBacking
            {
                Data =
                {
                    NewChart("c-late", sortOrder: 9),
                    NewChart("c-first", sortOrder: 0, isDefault: true),
                    NewChart("c-mid", sortOrder: 3),
                },
            };
            var store = BuildStore(backing);

            var result = store.Load();

            Assert.Equal(new[] { "c-first", "c-mid", "c-late" }, result.Select(d => d.Id).ToArray());
        }

        // --- Save ---

        [Fact]
        public void Save_NewDefinition_Persists()
        {
            var backing = new MemoryBacking();
            var store = BuildStore(backing);

            store.Save(NewChart("custom-1"));

            Assert.Contains(backing.Data, d => d.Id == "custom-1");
        }

        [Fact]
        public void Save_ExistingDefinition_ReplacesById()
        {
            var backing = new MemoryBacking();
            var store = BuildStore(backing);
            store.Save(NewChart("custom-1"));

            store.Save(new ChartDefinition { Id = "custom-1", Name = "renamed", SortOrder = 7 });

            var saved = backing.Data.Single(d => d.Id == "custom-1");
            Assert.Equal("renamed", saved.Name);
            Assert.Equal(7, saved.SortOrder);
        }

        [Fact]
        public void Save_WhenTargetIsDefault_Throws()
        {
            var store = BuildStore(new MemoryBacking());
            store.Load(); // 觸發種子

            var tampered = DefaultChartDefinitions.CreateEquipmentOverview();
            Assert.Throws<InvalidOperationException>(() => store.Save(tampered));
        }

        [Fact]
        public void Save_NewMarkedAsDefault_Throws()
        {
            var store = BuildStore(new MemoryBacking());

            Assert.Throws<InvalidOperationException>(() => store.Save(NewChart("evil", isDefault: true)));
        }

        [Fact]
        public void Save_WhenAtMaxCharts_Throws()
        {
            var backing = new MemoryBacking();
            backing.Data.Add(NewChart("default-1", sortOrder: 0, isDefault: true));
            for (var i = 1; i < ChartConstants.MaxCharts; i++)
                backing.Data.Add(NewChart($"custom-{i}", sortOrder: i));
            var store = BuildStore(backing);

            Assert.Throws<BusinessRuleException>(() => store.Save(NewChart("one-too-many")));
        }

        [Fact]
        public void Save_EmptyId_Throws()
        {
            var store = BuildStore(new MemoryBacking());

            Assert.Throws<InvalidOperationException>(() => store.Save(new ChartDefinition()));
        }

        // --- Delete ---

        [Fact]
        public void Delete_NormalChart_Removes()
        {
            var backing = new MemoryBacking();
            var store = BuildStore(backing);
            store.Save(NewChart("custom-1"));

            store.Delete("custom-1");

            Assert.DoesNotContain(backing.Data, d => d.Id == "custom-1");
        }

        [Fact]
        public void Delete_DefaultChart_Throws()
        {
            var store = BuildStore(new MemoryBacking());
            store.Load(); // 觸發種子

            Assert.Throws<InvalidOperationException>(
                () => store.Delete(DefaultChartDefinitions.EquipmentOverviewId));
        }

        [Fact]
        public void Delete_UnknownId_Throws()
        {
            var store = BuildStore(new MemoryBacking());

            Assert.Throws<InvalidOperationException>(() => store.Delete("no-such-id"));
        }
    }
}
