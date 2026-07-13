using System;
using System.Collections.Generic;
using System.Linq;
using FProductionDashBoard.Dtos;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 圖表定義淨化：剔除 FieldCatalog 已不存在的失效引用（欄位增減後的舊定義）。
    /// 僅在「設計器開啟」與「匯入」時執行並於儲存落檔；檢視端一律唯讀容忍、不改寫檔案。
    /// </summary>
    public static class ChartDefinitionSanitizer
    {
        /// <summary>就地剔除失效引用，回傳剔除數（0＝定義完好）</summary>
        public static int Sanitize(ChartDefinition def)
        {
            var removed = 0;

            if (!Enum.IsDefined(def.DataSet))
            {
                def.DataSet = ChartDataSet.Equipment;
                removed++;
            }

            if (!Enum.IsDefined(def.Container.Type))
            {
                def.Container.Type = ChartContainerType.Card;
                removed++;
            }

            var dataSet = def.DataSet;
            ChartFieldDescriptor? Find(string? fieldId)
                => string.IsNullOrEmpty(fieldId) ? null : FieldCatalog.Find(dataSet, fieldId);

            // 統計項：FieldId 空字串＝全部列計數（恆合法）；欄位失效或過濾值不在值清單→整項剔除
            removed += def.StatRow.Items.RemoveAll(item =>
            {
                if (string.IsNullOrEmpty(item.FieldId)) return false;
                var field = Find(item.FieldId);
                if (field == null) return true;
                return item.FilterValue != null && field.Values.All(v => v.Value != item.FilterValue);
            });

            // 篩選列
            removed += def.FilterRow.FilterFieldIds.RemoveAll(id => Find(id) == null);
            removed += def.FilterRow.QuickButtons.RemoveAll(b =>
            {
                var field = Find(b.FieldId);
                return field?.CanQuickFilter != true || field.Values.All(v => v.Value != b.Value);
            });
            removed += def.FilterRow.SortOptionFieldIds.RemoveAll(id => Find(id) == null);
            if (!string.IsNullOrEmpty(def.FilterRow.DefaultSortFieldId)
                && Find(def.FilterRow.DefaultSortFieldId) == null)
            {
                def.FilterRow.DefaultSortFieldId = "";
                removed++;
            }

            // 卡片容器
            var card = def.Container.Card;
            if (!string.IsNullOrEmpty(card.BorderColorFieldId) && Find(card.BorderColorFieldId) == null)
            {
                card.BorderColorFieldId = "";
                removed++;
            }
            if (!string.IsNullOrEmpty(card.TitleFieldId) && Find(card.TitleFieldId) == null)
            {
                card.TitleFieldId = "";
                removed++;
            }
            removed += card.Chips.RemoveAll(c => Find(c.FieldId) == null);
            removed += card.SecondaryFieldIds.RemoveAll(id => Find(id) == null);
            if (card.ProgressNumeratorFieldId != null && Find(card.ProgressNumeratorFieldId) == null)
            {
                card.ProgressNumeratorFieldId = null;
                removed++;
            }
            if (card.ProgressDenominatorFieldId != null && Find(card.ProgressDenominatorFieldId) == null)
            {
                card.ProgressDenominatorFieldId = null;
                removed++;
            }

            // 表格容器
            var table = def.Container.Table;
            removed += table.ColumnFieldIds.RemoveAll(id => Find(id) == null);
            if (table.IndicatorFieldId != null && Find(table.IndicatorFieldId) == null)
            {
                table.IndicatorFieldId = null;
                removed++;
            }

            return removed;
        }
    }
}
