using System;
using System.Collections.Generic;
using System.Linq;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services.Exceptions;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 本機 JSON 實作：單檔存全部定義（與 system_config.json 同資料夾）。
    /// JsonDataService 為無鎖靜態類，此處以內部 lock 保護讀寫；
    /// load/save 委派可注入供測試替換（比照 FileLicenseService 工廠模式），預設走 JsonDataService。
    /// </summary>
    public class JsonChartDefinitionStore : IChartDefinitionStore
    {
        private const string FileName = "chart_definitions.json";

        private readonly object _sync = new();
        private readonly Func<string, List<ChartDefinition>> _load;
        private readonly Action<List<ChartDefinition>, string> _save;
        private List<ChartDefinition>? _cache;

        public JsonChartDefinitionStore(
            Func<string, List<ChartDefinition>>? load = null,
            Action<List<ChartDefinition>, string>? save = null)
        {
            _load = load ?? JsonDataService.Load<List<ChartDefinition>>;
            _save = save ?? JsonDataService.Save;
        }

        public IReadOnlyList<ChartDefinition> Load()
        {
            lock (_sync)
            {
                _cache ??= _load(FileName) ?? new List<ChartDefinition>();
                EnsureSeeded();
                return _cache.OrderBy(d => d.SortOrder).ToList();
            }
        }

        public void Save(ChartDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
                throw new InvalidOperationException("[Save] 圖表定義 Id 不可為空");

            lock (_sync)
            {
                EnsureLoaded();

                var existing = _cache!.FirstOrDefault(d => d.Id == definition.Id);
                if (existing?.IsDefault == true || (existing == null && definition.IsDefault))
                    throw new InvalidOperationException("[Save] 預設圖表不可修改，僅可複製為新圖表");

                if (existing == null && _cache!.Count >= ChartConstants.MaxCharts)
                    throw new BusinessRuleException($"[Save] 圖表數已達上限 {ChartConstants.MaxCharts}，無法新增");

                if (existing != null)
                    _cache![_cache.IndexOf(existing)] = definition;
                else
                    _cache!.Add(definition);

                _save(_cache!, FileName);
            }
        }

        public void Delete(string id)
        {
            lock (_sync)
            {
                EnsureLoaded();

                var target = _cache!.FirstOrDefault(d => d.Id == id)
                    ?? throw new InvalidOperationException($"[Delete] 找不到圖表定義 Id={id}");
                if (target.IsDefault)
                    throw new InvalidOperationException("[Delete] 預設圖表不可刪除");

                _cache!.Remove(target);
                _save(_cache!, FileName);
            }
        }

        private void EnsureLoaded()
        {
            _cache ??= _load(FileName) ?? new List<ChartDefinition>();
            EnsureSeeded();
        }

        /// <summary>空清單種子全部預設；缺鎖定預設圖表時補回（確保現場永遠有畫面）</summary>
        private void EnsureSeeded()
        {
            var changed = false;

            if (_cache!.Count == 0)
            {
                _cache.AddRange(DefaultChartDefinitions.Create());
                changed = true;
            }
            else if (!_cache.Any(d => d.IsDefault))
            {
                _cache.Insert(0, DefaultChartDefinitions.CreateEquipmentOverview());
                changed = true;
            }

            if (changed)
                _save(_cache, FileName);
        }
    }
}
