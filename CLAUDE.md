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



\### 建立新業務邏輯服務



1\. 在FProductionDashBoard/Services 中建立新服務

2\. 建立對應的測試檔案





