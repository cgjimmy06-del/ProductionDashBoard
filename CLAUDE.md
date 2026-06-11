# 系統介紹

使用 WPF .NET8 開發現場生產管理操作系統，並連結公司資料庫

## 技術棧

- 框架：WPF .NET 8
- 語言：C# / XAML
- 建置工具：Visual Studio 2022
- 資料庫：SQL Server
- 套件管理：material design xaml(5.3) + CommunityToolkit.Mvvm(8.4)
- Node.js 版本：v24.14.1

## 專案架構

```
FProductionDashBoard/       # 視窗元件
├── Dtos/                   # 用於 CRUD 的資料串接
├── Models/                 # 對應資料表欄位的 Entity
├── Repositories/           # EF Core 對應資料表介面與實作
├── Resources/              # 適用於 XAML 的多國語言資源
├── Properties/             # 預設儲存與適用於程式碼的多國語言
├── Services/               # 業務邏輯服務
├── UserControls/           # 使用者控制項（用於視窗元件動態呼叫的介面）
├── ViewModels/             # 對應視窗元件的 ViewModel
├── Themes/                 # 適用於 XAML 的各式主題樣式
└── UiModels/               # 適用於 UI ViewModel 顯示的模型（對應 Entity）
DeviceDrivers.Abb/          # 適用於 ABB 的 SDK，用於串接機械手設備
DeviceDrivers.Abb.Tests/    # DeviceDrivers.Abb 的測試專案
FProductionDashBoard.Tests/ # FProductionDashBoard 的測試專案
LogUploadApi/               # IIS 系統相關 Web API
```

## 常用指令

## UI 樣式設計

- 分 Light 與 Dark 兩個主題
- 大部分套用 material design xaml 套件
- **介面設計原則：所有介面設計與優化皆以 `Themes/` 資料夾中定義的樣式為基礎**
  - 顏色引用優先使用 `Colors.Dark.xaml` / `Colors.Light.xaml` 中定義的自訂 Brush（如 `ErrorBrush`、`SuccessBrush`、`TextPrimaryBrush`），不使用 MaterialDesign 內建 Brush；配色以主色系與輔色系為出發（主要功能如確認、取消等用主色 `PrimaryBrush`，延伸功能如新增、刪除等用輔色 `SecondaryBrush`）
  - 元件樣式優先繼承 `Styles.Common.xaml` 的全域樣式，避免在個別 XAML 中寫 explicit `Style="{StaticResource MaterialDesign...}"`
  - 新增元件若需自訂樣式，應先在 `Styles.Common.xaml` 補充全域或具名樣式，再於 View 中引用
  - **Converter 必須集中在 `App.xaml` 的 `#region 自訂 Converter` 區塊統一宣告，禁止在各個 `UserControl.Resources` 中重複定義**

## 開發規範

### 命名規範

- 函式：PascalCase（`GetUserName`）
- private 與 protected 變數：camelCase（`userType`）
- public 變數：PascalCase（`UserType`）
- 檔案名稱：PascalCase（`UserProfile.cs`）
- 布林值：is/has 前綴（`isActive`）

### 錯誤處理

規則詳見 memory `feedback_log_format.md`，以下為摘要。

**A 類 — Service / Repository 層 throw**

```csharp
throw new InvalidOperationException("[方法名] 找不到 XXX ID={id}");
throw new DatabaseConnectionException("[方法名] 操作說明失敗：資料庫錯誤", sqlEx);
throw new BusinessRuleException("[方法名] 業務規則說明（中文）");
```

**B 類 — ViewModel catch**

```csharp
_core.Log.AddLog($"[{Info.Name}] 操作說明失敗", LogLevel.Error);   // 操作員看，中文，不含 ex.Message
_core.Log.AddErrorLog($"[方法名] {ex.Message}");                    // 工程師看，ex.Message 保留原始語言
```

**核心規則：**
1. 有 `AddErrorLog` 的地方必定有 `AddLog`（反之不一定）
2. `AddLog` 文字用中文，不含 `ex.Message`
3. `ex.Message` / `ex.StackTrace` 等系統產生內容保留原始語言
4. 失敗的 `AddLog` 必須明確標示 `LogLevel.Error`

### 語言資源完整性規則

新增或修改語言資源時，**必須同時補全三份**，禁止僅填其中一語言：

- `.resx` 三語言：`Resources.resx` / `Resources.zh-TW.resx` / `Resources.vi-VN.resx`
- StrResources XAML 三語言：`StrResources.zh-TW.xaml` / `StrResources.en-US.xaml` / `StrResources.vi-VN.xaml`

每次新增 key 後必須確認三份檔案 key 數一致，兩類語言資源（resx / StrResources）需分別同步確認。

> **⚠ resx 新增後必須手動更新 `Resources.Designer.cs`**：CLI build 不會觸發 resx 的 Designer 自動重新生成，需手動在 `Resources.Designer.cs` 補上對應的 `internal static string` property，格式如下：
> ```csharp
> internal static string MyNewKey {
>     get { return ResourceManager.GetString("MyNewKey", resourceCulture); }
> }
> ```

### EF Core 欄位對應

規則詳見 memory `feedback_efcore_column_mapping.md`。

- 新增 Entity 時，每個欄位必須明確寫 `HasColumnName("snake_case_name")`，不可依賴 EF 自動推導
- 漏寫時 EF 退回用 C# 屬性名（PascalCase）查詢，在區分大小寫（CS collation）的伺服器上會丟「無效的資料行」

```csharp
// 正確
builder.Property(e => e.IsCrossDay).HasColumnName("is_cross_day");

// 錯誤（漏寫 HasColumnName，只寫 HasColumnType）
builder.Property(e => e.IsCrossDay).HasColumnType("bit");
```

### Git 工作流程

規則詳見 memory `feedback_per_pr_new_branch.md`。

- 每個 PR 開工前必須先用 `feature-branch` skill 從 `develop` 建立**該 PR 專屬**的新分支
- 即使與前一個 PR 強相關，也不可在既有分支上接著開發
- 分支名格式：`feature/<功能簡稱>`（例：`feature/order-reception-service`）

### 建立新視窗或元件

1. 請在建立 window.xaml 或 user control 的同時建立對應的 ViewModel
2. 請建立對應的多國語言（中文、英文、越文）
3. 建立對應的測試檔案

### 建立新 Dialog（彈出操作視窗）

遇到「新增彈出視窗」、「新增操作 Dialog」等需求時，調用 memory `feedback_dialog_view_flow.md` 的 5 步 SOP。

- 核心規則：選項按鈕只反色更新選擇狀態，**不直接觸發確認**，必須由 DialogWindow 的確認按鈕輸出 Result
- 語言資源分兩處：ViewModel 程式碼用 `.resx`，XAML DynamicResource 用 `StrResources.*.xaml`

### 新增 Setting CRUD Tab

遇到「新增設定頁籤」、「新增 CRUD 管理介面」等需求時，調用 memory `feedback_setting_view_flow.md` 的 8 步 SOP。

### 建立新業務邏輯服務

1. 在 `FProductionDashBoard/Services` 中建立新服務
2. 建立對應的測試檔案

### 新增 NavMode（導覽列項目）

遇到「新增導覽列項目」、「新增頁面入口」等需求時，調用 memory `feedback_add_navmode_sop.md` 的 5 步 SOP。

必改的 5 個地方：`NavModePolicy.cs`（enum + Policy）→ `MainViewModel.SwitchPanelContent`（權限 + ViewModel）→ `MainWindow.xaml` RadioButton → `MainWindow.xaml` DataTemplate → 三個語言資源檔

### 新增離線寫入操作

遇到「需要在斷線時暫存寫入操作」等需求時，調用 memory `feedback_offline_service_sop.md` 的 6 步 SOP。

必改的 6 個地方：`PendingOperation.cs`（enum）→ `XxxPayload.cs`（DTO）→ `XxxSyncHandler.cs`（Handler）→ `DataServiceV1.cs`（離線路徑）→ `App.xaml.cs`（AddTransient 註冊）→ ViewModel catch `OfflineOperationQueuedException`
