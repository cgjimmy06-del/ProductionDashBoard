using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.ViewModels;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 圖表列的共用渲染管線：依 ChartDefinition 補完格式化字串與卡片槽位、彙總統計項。
    /// 檢視端（ChartViewModel 真實資料）與設計器左預覽（ChartSampleData 假資料）共用，
    /// 行為以檢視端為準、不各自複製。
    /// </summary>
    internal static class ChartRowBuilder
    {
        /// <summary>依定義補完單列：Display 格式化字串＋容器專屬槽位（卡片/表格燈號）</summary>
        internal static void FinishRow(ChartRowViewModel row, ChartDefinition def)
        {
            foreach (var field in FieldCatalog.For(def.DataSet))
                row.Display[field.FieldId] = FormatValue(field, row.Values.GetValueOrDefault(field.FieldId));

            if (def.Container.Type == ChartContainerType.Card)
                ApplyCardSlots(row, def);
            else if (def.Container.Type == ChartContainerType.Table)
                row.IndicatorBrushKey = ResolveValueBrushKey(
                    def.DataSet, def.Container.Table.IndicatorFieldId, row) ?? "IdleBrush";
        }

        /// <summary>依統計列設定彙總 rows；StatRow 停用時回空清單</summary>
        internal static List<ChartStatItemViewModel> BuildStatItems(
            ChartDefinition def, IReadOnlyCollection<ChartRowViewModel> rows)
        {
            var items = new List<ChartStatItemViewModel>();
            if (!def.StatRow.Enabled) return items;

            foreach (var item in def.StatRow.Items.Take(ChartConstants.MaxStatItems))
            {
                var value = item.Aggregate switch
                {
                    ChartAggregateType.Sum => rows.Sum(r => ToDouble(r.Values.GetValueOrDefault(item.FieldId))),
                    _ when string.IsNullOrEmpty(item.FieldId) => rows.Count,
                    _ when item.FilterValue == null =>
                        rows.Count(r => r.Values.GetValueOrDefault(item.FieldId) != null),
                    _ => rows.Count(r => string.Equals(
                             r.Values.GetValueOrDefault(item.FieldId)?.ToString(),
                             item.FilterValue, StringComparison.Ordinal)),
                };

                items.Add(new ChartStatItemViewModel(
                    ResolveLabel(item.LabelKey, item.Label),
                    value.ToString("N0", CultureInfo.CurrentCulture),
                    item.ColorKey ?? "TextPrimaryBrush"));
            }

            return items;
        }

        private static void ApplyCardSlots(ChartRowViewModel row, ChartDefinition def)
        {
            var card = def.Container.Card;
            row.Title = row.Display.GetValueOrDefault(card.TitleFieldId) ?? "";
            row.BorderBrushKey = ResolveValueBrushKey(def.DataSet, card.BorderColorFieldId, row) ?? "BorderBrush";

            foreach (var chip in card.Chips.Take(ChartConstants.MaxChips))
            {
                var field = FieldCatalog.Find(def.DataSet, chip.FieldId);
                if (field == null) continue;

                var raw = row.Values.GetValueOrDefault(chip.FieldId)?.ToString();
                var value = raw == null ? null : field.Values.FirstOrDefault(v => v.Value == raw);

                // Enum 值 rank==0 視為「無值」（None 類），配合「有值才顯示」不出 chip
                var hasValue = field.Type == ChartFieldType.Enum
                    ? value is { Rank: > 0 }
                    : !string.IsNullOrEmpty(raw);
                if (!hasValue && chip.ShowOnlyWhenHasValue) continue;

                row.Chips.Add(new ChartChipItem(
                    row.Display.GetValueOrDefault(chip.FieldId) ?? "",
                    value?.BrushKey ?? "IdleBrush"));
            }

            foreach (var fieldId in card.SecondaryFieldIds.Take(ChartConstants.MaxSecondaryInfos))
                row.SecondaryInfos.Add(new ChartSecondaryItem(
                    ResolveFieldLabel(def.DataSet, fieldId),
                    row.Display.GetValueOrDefault(fieldId) ?? "—"));

            if (card.ProgressNumeratorFieldId != null && card.ProgressDenominatorFieldId != null)
            {
                var num = ToDouble(row.Values.GetValueOrDefault(card.ProgressNumeratorFieldId));
                var den = ToDouble(row.Values.GetValueOrDefault(card.ProgressDenominatorFieldId));
                row.HasProgress = true;
                row.ProgressPercent = den > 0 ? Math.Clamp(num / den * 100, 0, 100) : 0;
                row.ProgressText = den > 0
                    ? $"{num.ToString("N0", CultureInfo.CurrentCulture)} / {den.ToString("N0", CultureInfo.CurrentCulture)}"
                    : "— / —";
            }
        }

        internal static string FormatValue(ChartFieldDescriptor field, object? raw)
        {
            if (raw == null) return "—";
            return field.Type switch
            {
                ChartFieldType.Enum   => ResolveEnumLabel(field, raw.ToString() ?? ""),
                ChartFieldType.Number => Convert.ToDouble(raw, CultureInfo.InvariantCulture)
                                             .ToString("N0", CultureInfo.CurrentCulture),
                ChartFieldType.Date   => ((DateTime)raw).ToString("yyyy-MM-dd"),
                _                     => raw.ToString() ?? "—",
            };
        }

        internal static string ResolveEnumLabel(ChartFieldDescriptor field, string value)
        {
            var v = field.Values.FirstOrDefault(x => x.Value == value);
            return v == null ? value
                : Properties.Resources.ResourceManager.GetString(v.LabelKey) ?? value;
        }

        /// <summary>取列在指定 Enum 欄位當前值的狀態色 key（FieldCatalog 值宣告）；查無回 null</summary>
        internal static string? ResolveValueBrushKey(ChartDataSet dataSet, string? fieldId, ChartRowViewModel row)
        {
            if (string.IsNullOrEmpty(fieldId)) return null;
            var field = FieldCatalog.Find(dataSet, fieldId);
            var raw = row.Values.GetValueOrDefault(fieldId)?.ToString();
            return field?.Values.FirstOrDefault(v => v.Value == raw)?.BrushKey;
        }

        internal static string ResolveLabel(string? labelKey, string fallback)
            => labelKey != null
                ? Properties.Resources.ResourceManager.GetString(labelKey) ?? fallback
                : fallback;

        internal static string ResolveFieldLabel(ChartDataSet dataSet, string fieldId)
        {
            var field = FieldCatalog.Find(dataSet, fieldId);
            return field == null ? fieldId
                : Properties.Resources.ResourceManager.GetString(field.LabelKey) ?? fieldId;
        }

        /// <summary>快捷按鈕標籤：LabelKey > 自訂 Label > 值標籤（FieldCatalog）</summary>
        internal static string ResolveQuickButtonLabel(ChartFieldDescriptor field, QuickFilterButtonConfig cfg)
            => cfg.LabelKey != null
                ? Properties.Resources.ResourceManager.GetString(cfg.LabelKey) ?? cfg.Value
                : !string.IsNullOrEmpty(cfg.Label) ? cfg.Label : ResolveEnumLabel(field, cfg.Value);

        internal static double ToDouble(object? raw)
        {
            if (raw == null) return 0;
            try
            {
                return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;   // 非數值（如 Sum 誤配 Enum 欄位的舊定義）以 0 容忍，不崩潰
            }
        }
    }
}
