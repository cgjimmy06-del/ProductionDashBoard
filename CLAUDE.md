\# 系統介紹



使用WPF .NET8 開發現場生產管理操作系統，並連結公司資料庫



\## 技術棧



\- 框架：WPF .NET 8

\- 語言：C# / XAML

\- 建置工具：Visual Studio 2022

\- 資料庫：SQL Server

\- 套件管理：material design xaml(5.3) + CommunityToolkit.Mvvm(8.4)

\- Node.js 版本：v24.14.1



\## 專案架構



FProductionDashBoard/ 	# 視窗元件

├── Models/    			# 對應資料表欄位的Entity

├── Repositories/  		# EF Core對應資料表介面與實作

├── Resources/   		# 適用於XAML的多國語言資源

├── Properties/     	# 預設儲存與適用於程式碼的多國語言

├── Services/      		# 業務邏輯服務

├── UserControls/      		#  使用者控制項 (用於視窗元件動態呼叫的介面)

├── ViewModels/      		#  對應視窗元件的ViewModel

├── Themes/       		# 適用於XAML的各式主題樣式

└── UiModels/      		# 適用於UI view model顯示的模型 (對應entity)



\## 常用指令



\## UI樣式設計



\- 分Light與Dark兩個主題

\- 大部分套用material design xaml套件

\- **介面設計原則：所有介面設計與優化皆以 `Themes/` 資料夾中定義的樣式為基礎**

  \- 顏色引用優先使用 `Colors.Dark.xaml` / `Colors.Light.xaml` 中定義的自訂 Brush（如 `ErrorBrush`、`SuccessBrush`、`TextPrimaryBrush`），不使用 MaterialDesign 內建 Brush；配色以主色系與輔色系為出發（主要功能如確認、取消等用主色 `PrimaryBrush`，延伸功能如新增、刪除等用輔色 `SecondaryBrush`）

  \- 元件樣式優先繼承 `Styles.Common.xaml` 的全域樣式，避免在個別 XAML 中寫 explicit `Style="{StaticResource MaterialDesign...}"`

  \- 新增元件若需自訂樣式，應先在 `Styles.Common.xaml` 補充全域或具名樣式，再於 View 中引用



\## 開發規範



\### 命名規範



\- 函式：PascalCase（`getUserName`）

\- private與producted變數：camelCase（userType）

\- public變數：PascalCase（UserType）

\- 檔案名稱：PascalCase（`UserProfile.cs`）

\- 布林值：is/has 前綴（isActive）



\### 錯誤處理







\### 建立新視窗或元件



1. 請在建立window.xaml或user control的同時建立對應的view model
2. 請建立對應的多國語言 (中文、英文、越文)
3. 建立對應的測試檔案



\### 建立新 Dialog（彈出操作視窗）

遇到「新增彈出視窗」、「新增操作 Dialog」等需求時，調用 memory `feedback_dialog_view_flow.md` 的 5 步 SOP。

\- 核心規則：選項按鈕只反色更新選擇狀態，**不直接觸發確認**，必須由 DialogWindow 的確認按鈕輸出 Result

\- 語言資源分兩處：ViewModel 程式碼用 `.resx`，XAML DynamicResource 用 `StrResources.*.xaml`



\### 新增 Setting CRUD Tab

遇到「新增設定頁籤」、「新增 CRUD 管理介面」等需求時，調用 memory `feedback_setting_view_flow.md` 的 8 步 SOP。



\### 建立新業務邏輯服務



1\. 在FProductionDashBoard/Services 中建立新服務

2\. 建立對應的測試檔案





