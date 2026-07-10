using System.Collections.Generic;
using FProductionDashBoard.Dtos;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 圖表定義存取層：本機 JSON 起步，未來全廠共用時換 DB 實作即可（設計器與渲染端不動）。
    /// </summary>
    public interface IChartDefinitionStore
    {
        /// <summary>載入全部定義（依 SortOrder 排序）；無檔/空清單自動種子預設圖表</summary>
        IReadOnlyList<ChartDefinition> Load();

        /// <summary>新增或更新（依 Id upsert）；預設圖表不可修改、超出上限時 throw</summary>
        void Save(ChartDefinition definition);

        /// <summary>刪除；預設圖表或不存在的 Id 時 throw</summary>
        void Delete(string id);
    }
}
