namespace FProductionDashBoard.Constants
{
    // SOP 點檢明細依 CheckType 篩選 Material 時用的硬編碼 TypeId。
    // 對應 material_type 表中由開發者手動 seed 的兩筆固定列。
    // 若 SSMS 中該表被改動須同步修此檔。
    public static class MaterialTypeIds
    {
        public const int Station = 1; // 工位類物料（CheckType.Station 用）
        public const int Fixture = 2; // 治夾具類物料（CheckType.Fixture 用）
    }
}
