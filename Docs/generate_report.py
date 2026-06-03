# -*- coding: utf-8 -*-
"""
FProductionDashBoard Code Review Report Generator
Outputs: Excel (.xlsx), Word (.docx), PDF (via Word COM)
"""

import os
import sys
from datetime import date
from pathlib import Path

OUTPUT_DIR = Path(r"C:\AIresources\DB_outputFile")
VERSION_TAG = "v2.5"
XLSX_PATH = OUTPUT_DIR / f"CodeReview_FProductionDashBoard_{VERSION_TAG}.xlsx"
DOCX_PATH = OUTPUT_DIR / f"CodeReview_FProductionDashBoard_{VERSION_TAG}.docx"
PDF_PATH  = OUTPUT_DIR / f"CodeReview_FProductionDashBoard_{VERSION_TAG}.pdf"
REVIEW_DATE = date.today().strftime("%Y-%m-%d")

# ─────────────────────────────────────────
# DATA — 43 findings
# ─────────────────────────────────────────
FINDINGS = [
    # ID, Severity, Category, File, Lines, Method/Scope, Description, Fix, Phase, Status
    ("SEC-01","Critical","安全性","appsettings.json","3-8","—",
     "連線字串含明文 SA 密碼（P@ssw0rd / vsmis20090515），硬編碼在版本控制中",
     "改用 User Secrets / 環境變數 / Azure Key Vault，從版本控制中移除明文密碼","Phase 4","待修"),

    ("SEC-02","Critical","安全性","LoginDapper.cs / DataServiceV1.cs","54 / 516 / 533","validateUser / AddEmployeeAsync",
     "員工密碼以明文方式存取資料庫，無任何雜湊或加鹽處理",
     "使用 bcrypt 或 Argon2 對密碼進行雜湊後儲存，驗證時比對雜湊值","Phase 4","待修"),

    ("SEC-03","Critical","安全性","LoginDapper.cs","54-89","validateUser",
     "密碼驗證無帳號鎖定機制、無登入嘗試次數限制、無 bcrypt/Argon2",
     "加入登入失敗計數器，超過閾值後鎖定帳號並記錄","Phase 4","待修"),

    ("C2","High","執行緒安全","DeviceCardContainerViewModel.cs","169, 191","AddDeviceCard / FastUploadDevices",
     "await UpdateTimeSlotsStatusAsync() 後，continuation 可能在執行緒池執行，Devices.Add() 違反 ObservableCollection 單執行緒規則",
     "在 Devices.Add/Clear 前包裹 Application.Current.Dispatcher.Invoke()","Phase 1","已修正 PR#20"),

    ("C3","High","記憶體洩漏","MainViewModel.cs","118, 149","建構子",
     "CardRead / UserChanged 事件訂閱（+=）未取消，匿名 Lambda 無法 -=，無 IDisposable",
     "實作 IDisposable，Lambda 存為具名欄位，Dispose() 中執行 -=","Phase 1","已修正 PR#20"),

    ("C3b","High","記憶體洩漏","DeviceCardContainerViewModel.cs","49","建構子",
     "UserChanged 匿名 Lambda 無法 -=，無 IDisposable",
     "同 C3，實作 IDisposable 並存為具名欄位","Phase 1","已修正 PR#20"),

    ("C4","High","資料遺失","DataServiceV1.cs","206/251/293/399/428","Add*Async 系列",
     "5 處 `_ = Task.Run(async () => await EnqueueAsync(op))` fire-and-forget，若 EnqueueAsync 失敗資料遺失",
     "改為 `await _offlineCache.EnqueueAsync(op).ConfigureAwait(false)` 後再 throw","Phase 1","已修正 PR#20"),

    ("H2","High","執行緒","MainViewModel.cs","124-127","建構子",
     "DefaultTimer 在建構子啟動，無 Stop()，應用程式關閉後仍持續 Tick",
     "在 IDisposable.Dispose() 中呼叫 DefaultTimer.Stop()","Phase 1","已修正 PR#20"),

    ("H2b","High","執行緒","DeviceCardViewModel.cs","290","EndTuningAsync",
     "使用者取消時 `if (!confirmed) return` 未呼叫 _tuningTimer?.Stop()，計時器繼續執行",
     "改為 `if (!confirmed) { _tuningTimer?.Stop(); return; }`","Phase 1","已修正 PR#20"),

    ("H3","High","競態","DataServiceV1.cs","181-430","Add*Async 系列 (6 個方法)",
     "CheckConnectionAsync 通過後網路中斷，catch 區塊僅處理 SqlException，未處理 TimeoutException",
     "在 online 路徑的 catch 補加 TimeoutException，fallback 至離線路徑","Phase 1","已修正 PR#20"),

    ("B1","High","業務邏輯","CardReaderHandler.cs","32","OnCardRead",
     "多台讀卡機幾乎同時觸發時，InitializeAsync 可能被並行呼叫兩次，導致重複登入或重複新增員工",
     "加入 SemaphoreSlim(1,1) + WaitAsync(0) 非阻塞鎖，確保同一時間只有一個 OnCardRead 邏輯執行","Phase 4","已修正 PR#37"),

    ("UI-01","High","MVVM 違規","MainViewModel.cs","323, 344, 477, 505","多個方法",
     "ViewModel 直接 new DialogWindow / LoginWindow 並呼叫 ShowDialog()，嚴重違反 MVVM 分層",
     "引入 IDialogService，ViewModel 僅呼叫 Service 介面，不持有 Window 參考","Phase 3","已修正 PR#23"),

    ("UI-01b","High","MVVM 違規","DeviceCardViewModel.cs / DeviceCardContainerViewModel.cs","多處","多個方法",
     "合計 15+ 個 ShowDialog() 直接呼叫，同 UI-01 問題，影響範圍更大",
     "統一改用 IDialogService","Phase 3","已修正 PR#23"),

    ("M1","Medium","效能","Services/ + Repositories/","全域","~197 個 await",
     "Service 層與 Repository 層所有 await 均缺少 .ConfigureAwait(false)，可能造成不必要的 UI 執行緒等待",
     "在 Services/ 與 Repositories/ 的每個 await 加上 .ConfigureAwait(false)；ViewModel 層不加","Phase 1","已修正 PR#20"),

    ("B2","Medium","業務邏輯","DeviceCardViewModel.cs","262","EndTuningAsync",
     "區域 bool confirmed 可能跨執行緒讀寫，無 volatile 保護",
     "改為 `volatile bool confirmed` 或使用 Interlocked","Phase 2","已修正 PR#21"),

    ("B3","Medium","業務邏輯","DataServiceV1.cs","327-358","CheckAndInsertMissedInspectionAsync",
     "逐筆查詢後插入補填，無事務保護，中途失敗會產生部分插入狀態，可能造成重複記錄",
     "改用 MERGE / INSERT IF NOT EXISTS，或整批包裹在 Transaction 中","Phase 2","已修正 PR#21"),

    ("B4","Medium","業務邏輯","OfflineSyncService.cs","全域","SyncPendingAsync",
     "同一離線操作若被排隊兩次，上傳後資料庫會出現重複記錄，缺乏冪等性 Key",
     "在 PendingOperation 加入 IdempotencyKey，上傳前檢查是否已存在","Phase 2","已修正 PR#21"),

    ("A4","Medium","架構 / DI","App.xaml.cs","83","DI 配置",
     "AuthorizationService 為 Singleton，但依賴 RolePermissionRepository(Scoped)，形成 Captive Dependency",
     "AuthorizationService 改用快取機制（SetCachedRoles），移除 RolePermissionRepository 依賴","Phase 2","已修正 PR#34"),

    ("A4b","Medium","架構 / DI","App.xaml.cs","70-73","DI 配置",
     "LocalDbContext 設為 Singleton，違反 EF Core 最佳實踐，多並發請求共享同一 DbContext",
     "改為 AddDbContextFactory<LocalDbContext>，各操作自行建立短命 context","Phase 2","已修正 PR#21"),

    ("A1","Medium","架構","MainViewModel.cs","多處","建構子",
     "直接 new DeviceCardContainerViewModel() / new SettingViewModel()，繞過 DI，無法測試與替換",
     "改用 IServiceProvider.GetRequiredService<T>() 或 factory pattern","Phase 3","已修正 PR#23"),

    ("SEC-04","Medium","安全性","App.xaml.cs","121-125","OnStartup catch",
     "啟動失敗時 MessageBox 顯示完整 StackTrace、系統路徑，可能洩露敏感環境資訊",
     "只顯示友善訊息，詳細錯誤寫入 LogService，不暴露給使用者","Phase 2","已修正 PR#37"),

    ("SEC-05","Medium","安全性","LoginDapper.cs","43, 51","catch 區塊",
     "Debug.WriteLine 輸出完整 SqlException 訊息，在 Debug 模式下可能洩露 SQL 語法",
     "屬硬體驅動層，無 LogService 注入管道；Debug.WriteLine 已足夠","Phase 2","不修正"),

    ("ERR-01","Medium","錯誤處理","OfflineSyncService.cs","44","SyncPendingAsync",
     "空 catch block，離線同步失敗原因完全不記錄，維運人員無從排查",
     "無 LogService 注入管道，Debug.WriteLine 已足夠","Phase 2","不修正"),

    ("ERR-02","Medium","錯誤處理","AuthorizationService.cs","58","InitializeAsync",
     "空 catch block，降級為訪客使用者但無任何日誌，故障時無法追蹤",
     "架構重構（PR#34 cache-based）後已無 catch 路徑","Phase 2","已解決 PR#34"),

    ("ERR-03","Medium","錯誤處理","CardReaderService.cs","33, 44, 76","Start / Stop / DataReceived",
     "串口操作失敗僅 Debug.WriteLine，生產環境不可見，無法追蹤讀卡機斷線原因",
     "純硬體驅動層，無 LogService 注入管道，錯誤由上層 CardReaderHandler 捕捉","Phase 2","不修正"),

    ("UI-02","Medium","硬編碼字串","ViewModels/","14 個檔案 201 處","多個方法",
     "201 處硬編碼中文字串散落在 ViewModel，無法切換語言",
     "全部移至 .resx 資源檔，使用 Properties.Resources.XXX 引用","Phase 4","待修"),

    ("UI-03","Medium","MVVM 品質","TimeSlot/Employee/InspectionDialog","6 處","OnXxxChanged",
     "手動呼叫 OnPropertyChanged(nameof(X))，已有 [NotifyPropertyChangedFor] 屬性可替代",
     "改用 [NotifyPropertyChangedFor(nameof(X))] 標註 ObservableProperty","Phase 4","已修正 PR#37"),

    ("PROD-01","Medium","生產環境","DataServiceV1.cs / MainViewModel.cs","54-99 / 96","Demo() / TestCommand",
     "Demo() 方法與 TestCommand 暴露在生產環境，IDataService 公開介面包含 Demo()",
     "加入 #if DEBUG 條件編譯，或完全移除","Phase 3","已修正 PR#22"),

    ("B5","Medium","業務邏輯","AuthorizationService.cs","全域","LogoutAsync",
     "訪客使用者 Id = 2 硬編碼，登出期間刷卡仍可觸發登入流程",
     "訪客 Id 從 appsettings 讀取；登出時配合 B1 的 SemaphoreSlim 屏蔽 CardRead","Phase 3","待修"),

    ("B8","Medium","業務邏輯","DataServiceV1.cs","GetSlotBounds","GetSlotBounds",
     "跨日時段（23:59:59 → 00:00:00）邊界計算邏輯缺乏單元測試，邊界錯誤無法被及早發現",
     "在 InspectionRecordRepository 補充跨日邊界的單元測試","Phase 4","待補測試"),

    ("PATH-01","Medium","路徑管理","DeviceCardContainerViewModel.cs / JsonDataService.cs","21","defaultDevicesFile / baseFolder",
     "JSON 資料硬編碼，未使用 AppData 路徑",
     "改用 %LOCALAPPDATA%\\FProductionDashBoard\\，App.xaml.cs 啟動時建立目錄","Phase 4","已修正 PR#36"),

    ("PATH-02","Medium","路徑管理","App.xaml.cs","76","local_cache.db",
     "SQLite cache 寫入 exe 旁，發佈時可能無寫入權限",
     "改用 %LOCALAPPDATA%\\FProductionDashBoard\\local_cache.db","Phase 4","已修正 PR#36"),

    ("TEST-01","Medium","測試覆蓋","FProductionDashBoard.Tests/","全域","—",
     "OnCardRead 端到端流程、EndTuningAsync、業務對話框完整流程完全無測試，共 15 個關鍵區塊",
     "補充整合測試：OnCardRead 換手、EndTuningAsync 計時結束、離線冪等性","Phase 4","待補測試"),

    ("BUG-01","Medium","Bug","LogService.cs","153","CleanupOldLogs",
     "字串字面量錯誤：\"{filetitle}_*.txt\" 應為 $\"{filetitle}_*.txt\"，導致舊日誌無法被正確清理",
     "修正為插值字串 $\"{filetitle}_*.txt\"","Phase 1","已修正 PR#20"),

    ("UI-04","Low","程式碼品質","InspectionDialogViewModel.cs","52","建構子",
     "var categories = new List<int> { 1, 2 }，魔術數字，語義不明",
     "定義 InspectionCategory Enum 或具名常數取代","Phase 3","已修正 PR#22"),

    ("ERR-04","Low","錯誤處理","LogService.cs","172-175","CleanupOldLogs",
     "檔案刪除失敗的空 catch，舊日誌未清理且無通知",
     "不可呼叫自身，刪除舊 log 失敗屬可忽略次要操作","Phase 3","不修正"),

    ("ERR-05","Low","錯誤處理","DeviceCardViewModel.cs","313-315","EndTuningAsync",
     "EndTuningAsync 例外只記錄日誌，使用者介面無任何錯誤提示",
     "已有 AddLog + AddErrorLog 雙寫格式","Phase 3","已解決"),

    ("A5","Low","測試覆蓋","FProductionDashBoard.Tests/","全域","—",
     "缺少 Race Condition 並發場景測試、離線冪等性測試、CleanupOldLogs 測試",
     "補充對應的 xUnit 測試","Phase 4","待補測試"),

    ("PROD-02","Low","生產環境","MainViewModel.cs","528-566","SqlTestFunc",
     "SqlTestFunc() 含大量測試程式碼殘留，透過 TestCommand 可在生產環境執行",
     "加入 #if DEBUG 或完全刪除","Phase 3","已修正 PR#22"),

    ("PROD-03","Low","生產環境","CardReaderEntryViewModel.cs","57-67","Test()",
     "Test() 命令公開，建立臨時 SerialPort 測試連線，功能應整合至正式流程",
     "整合至 HardwareViewModel 的正式連線測試流程","Phase 3","已修正 PR#22"),

    ("UI-05","Low","安全性","LoginViewModel.cs / CardReaderHandler.cs","82 / 81","初始化 / HandleAddNewEmployeeAsync",
     "Password = \"0000\" 硬編碼預設密碼暴露在原始碼中",
     "移除硬編碼，改為由管理員指定或要求首次登入變更","Phase 4","待修"),

    ("CONF-01","Low","架構","App.xaml.cs","全域","DI 配置",
     "IDataService 直接暴露所有 Repository 方法，破壞服務封裝，依賴關係過於寬鬆",
     "依職責拆分為 IInspectionService、IMaterialService 等細粒度介面","Phase 3","已修正 PR#22"),

    ("ARCH-01","Low","架構","全域","—","LocalizedDescriptionAttribute",
     "LocalizedDescriptionAttribute 放置位置不當，應移至 Models 或 Attributes 目錄",
     "移至 FProductionDashBoard/Attributes/ 並更新引用","Phase 3","暫緩"),

    ("THREAD-01","Low","執行緒","LoadingViewModel.cs","30","建構子",
     "CancellationTokenSource 建立後未實作 IDisposable，應用程式關閉時可能未釋放",
     "實作 IDisposable，Dispose() 中呼叫 _cts.Cancel() 與 _cts.Dispose()","Phase 2","已修正 PR#21"),

    # ────────────────────────────────────────────
    # Phase 5 二輪審查（2026-05-15）— 18 項新發現
    # 針對 PR #20~#37 後新增/修改程式碼的審查
    # ────────────────────────────────────────────
    ("IDLE-01","Medium","UX / 執行緒","MainViewModel.cs","117, 331","OnTimerTickAsync",
     "_logInOutCounter 固定倒數，無偵測真實使用者互動",
     "改用 Win32 GetLastInputInfo P/Invoke，偵測 OS 級別輸入時間","Phase 4","已修正 PR#37"),

    ("P5-H1","High","資源洩漏","LogUploadService.cs / SystemSettingsViewModel.cs","17-44 / 188-194","UploadLog*Async / UploadLogAsync",
     "MultipartFormDataContent / StreamContent 漏 using，多次上傳累積記憶體",
     "改為 using var content / using var fileContent，確保正確 Dispose","Phase 4","待修"),

    ("P5-H2","High","異常處理","App.xaml.cs","135","OnStartup",
     "空 catch 吞噬 GetAllRolesAsync 例外，權限載入失敗無提示",
     "改為 catch (Exception ex) { Debug.WriteLine($\"[OnStartup.LoadRoles] {ex.Message}\"); }","Phase 4","待修"),

    ("P5-H3","High","安全 / 死碼","EncryptionAndHashService.cs","33-40","static ctor",
     "AES Key/IV 每次啟動隨機生成，且 Debug.WriteLine 印金鑰；未被任何程式碼使用",
     "直接刪除 EncryptionService 死碼類別；若日後實作 SEC-02 從 Windows Credential Manager 讀取持久金鑰","Phase 4","待修"),

    ("P5-H4","High","DRY","DataServiceV1.cs / InspectionRecordRepository.cs","366-377 / 26-35","GetSlotBounds",
     "跨日時段邊界邏輯（IsCrossDay 處理 slotStart/slotEnd）在 3 個地方重複",
     "抽取為 TimeSlotLookup 擴充方法或 TimeSlotHelper 共用工具","Phase 4","待修"),

    ("P5-H5","High","異常 / 可觀察性","DataServiceV1.cs","181, 231, 291, 406, 440","Add*Async 系列",
     "Fire-and-forget Task.Run 例外只寫 Debug.WriteLine，生產環境看不到",
     "DataServiceV1 注入 LogService，catch 改寫 LogService.AddErrorLog","Phase 4","待修"),

    ("P5-M1","Medium","事件洩漏","LoadingWindow.xaml.cs","19","建構子",
     "vm.CloseRequested += lambda 訂閱無 -=，多次開關 Loading 累積",
     "改用具名 handler，Closed 時 -= 取消訂閱","Phase 4","待修"),

    ("P5-M2","Medium","事件洩漏","MultiCardReaderService.cs","15-27","AddReader / RemoveReader",
     "RemoveReader 不取消 CardRead 訂閱（用 lambda 無法移除）",
     "用 Dictionary<Reader, EventHandler> 保留 handler，Remove 時 -=","Phase 4","待修"),

    ("P5-M3","Medium","事件洩漏","DialogWindow.xaml.cs","31-32","建構子",
     "RequestClose += this.Close 訂閱無 -=",
     "改用具名 handler，Closed 時 -= 取消訂閱","Phase 4","待修"),

    ("P5-M4","Medium","記憶體","SystemSettingsViewModel.cs","217-250","ApplyLanguage / ApplyTheme",
     "ResourceDictionary 切換用索引覆寫，舊字典仍被 BindingExpression 持有",
     "先 RemoveAt(index) 再 Insert(index, dict)，確保 GC 可回收","Phase 4","待修"),

    ("P5-M5","Medium","資源","DeviceCardViewModel.cs","247-254","TuningAsync",
     "_tuningTimer 重啟時舊 reference 未清空，重新賦值前若例外可能殘留參考",
     "賦新值前先設 _tuningTimer = null","Phase 4","待修"),

    ("P5-M6","Medium","安全","SystemSettingsViewModel.cs","188-191","UploadLogAsync",
     "AttachmentPaths 直接拼進 ZIP，缺白名單與 Path.GetFullPath 驗證",
     "限制副檔名白名單 + Path.GetFullPath 驗證在允許目錄內","Phase 4","待修"),

    ("P5-M7","Medium","業務邏輯","InspectionRecordRepository.cs","155","查詢方法",
     "r.CreateAt == date.Date 用 ==，與其他查詢用區間 >= && < 不一致",
     "統一改為 r.CreateAt >= date.Date && r.CreateAt < date.Date.AddDays(1)","Phase 4","待修"),

    ("P5-L1","Low","品質","DataServiceV1.cs / ReplacementSyncHandler.cs","160, 207, 220, 252 / 21","多處",
     "硬編碼錯誤碼字串（\"MTRP0001\" / \"RTIN0001\" 等）",
     "移至命名常數或共用 ErrorCodes 類別","Phase 4","待修"),

    ("P5-L2","Low","品質","BaseRepository.cs","44","建構子",
     "new CancellationTokenSource(2000) Timeout 硬編碼",
     "改為命名常數 const int RepoTimeoutMs = 2000","Phase 4","待修"),

    ("P5-L3","Low","品質","CardReaderService.cs","19, 29","建構子",
     "COM3 / 115200 預設值與 Settings.settings 重複",
     "從 Settings.Default.ReaderPort / ReaderBaud 讀取","Phase 4","已修正 PR#62"),

    ("P5-L4","Low","品質","DataServiceV1.cs","18","using 區",
     "using static System.Reflection.Metadata.BlobBuilder; 未使用",
     "刪除未使用的 using","Phase 4","待修"),

    ("P5-L5","Low","品質","MainViewModel.cs","351","SyncAndLogAsync",
     "// TODO: 根據 result.SyncedCount / result.FailedCount 決定 log 輸出時機",
     "完成 TODO 或移至 Issue 追蹤","Phase 4","待修"),

    ("P5-L6","Low","品質","RoutineInspectionSyncHandler.cs","20-25","HandleAsync",
     "註解掉的舊重複檢查（B4b 已在 DataServiceV1 上游處理）",
     "刪除註解段","Phase 4","待修"),
]

TESTS_LIST = [
    "HandlerTests.ReplacementSyncHandler_DeserializesPayloadAndCallsRepo",
    "HandlerTests.FirstInspectionSyncHandler_DeserializesPayloadAndCallsRepo",
    "HandlerTests.RoutineInspectionSyncHandler_DeserializesPayloadAndCallsRepo",
    "OfflineCacheServiceTests.EnqueueAsync_PersistsOperation",
    "OfflineCacheServiceTests.EnqueueAsync_DefaultRetryCount_IsZero",
    "OfflineCacheServiceTests.HasPendingAsync_WhenEmpty_ReturnsFalse",
    "OfflineCacheServiceTests.HasPendingAsync_WhenExists_ReturnsTrue",
    "OfflineCacheServiceTests.GetPendingAsync_ReturnsAllOperations",
    "OfflineCacheServiceTests.GetPendingAsync_OrderedByCreatedAt",
    "OfflineCacheServiceTests.MarkSyncedAsync_RemovesOperation",
    "OfflineCacheServiceTests.MarkFailedAsync_IncrementsRetryCount",
    "OfflineSyncServiceTests.SyncPendingAsync_WhenNoPending_ReturnsEmptyAndDoesNotCreateScope",
    "OfflineSyncServiceTests.SyncPendingAsync_WhenConnectionFails_ReturnsEmptyAndDoesNotProcessOps",
    "OfflineSyncServiceTests.SyncPendingAsync_WhenHandlerSucceeds_ReturnsSyncedCountOne",
    "OfflineSyncServiceTests.SyncPendingAsync_WhenHandlerFails_ReturnsFailedCountAndContinues",
    "OfflineSyncServiceTests.SyncPendingAsync_WhenNoHandlerFound_SkipsOperation",
    "LogServiceTests.LoadLogFile_NonExistentFile_ReturnsErrorMessage",
    "LogServiceTests.LoadLogFile_ExistingFile_ReturnsFileContent",
    "LogServiceTests.SaveAllLogsToFileAsync_CreatesLogAndErrorLogFiles",
    "LogServiceTests.RefreshAvailableLogFiles_PopulatesListWithMatchingFiles",
    "LogServiceTests.RefreshAvailableLogFiles_DoesNotIncludeNonMatchingFiles",
    "AuthorizationServiceTests.InitializeAsync_SetsCurrentUser",
    "AuthorizationServiceTests.InitializeAsync_LoadsPermissionsFromRole",
    "AuthorizationServiceTests.InitializeAsync_UnknownRoleId_GrantsNoPermissions",
    "AuthorizationServiceTests.InitializeAsync_RaisesUserChangedEvent",
    "AuthorizationServiceTests.HasPermission_WithMatchingPermissionId_ReturnsTrue",
    "AuthorizationServiceTests.HasPermission_WithNonMatchingPermissionId_ReturnsFalse",
    "AuthorizationServiceTests.IsLoggedIn_RealUser_ReturnsTrue",
    "AuthorizationServiceTests.IsLoggedIn_VisitorUser_ReturnsFalse",
    "AuthorizationServiceTests.InitializeAsync_DbThrows_FallsBackToVisitor",
    "AuthorizationServiceTests.LogoutAsync_SetsCurrentUserToVisitor",
    "AuthorizationServiceTests.LogoutAsync_RaisesUserChangedEvent",
    "DataServiceV1Tests.GetAllSlotsStatusAsync (9 tests)",
    "DataServiceV1Tests.AddRoutineInspectionAsync (3 tests)",
    "DataServiceV1Tests.AddFirstInspectionAsync (4 tests)",
    "DataServiceV1Tests.AddReplacementRecordAsync (3 tests)",
    "DataServiceV1Tests.CheckAndInsertMissedInspectionAsync (8 tests)",
    "DataServiceV1Tests.GetCurrentTimeSlotIdAsync (2 tests)",
    "DataServiceV1Tests.Role CRUD (6 tests)",
    "TuningDialogViewModelTests.TeachingCommand_SetsConfirmedWithTeachingResult",
    "TuningDialogViewModelTests.OffsetCommand_SetsConfirmedWithOffsetResult",
    "TuningDialogViewModelTests.CancelCommand_SetsNotConfirmedNullResult",
    "CardReaderServiceTests.TryExtractCardId_CrTerminator_ReturnsCardId",
    "CardReaderServiceTests.TryExtractCardId_LfTerminator_ReturnsCardId",
    "CardReaderServiceTests.TryExtractCardId_CrLfTerminator_ReturnsCardId",
    "CardReaderServiceTests.TryExtractCardId_IncompleteData_ReturnsNullAndPreservesBuffer",
    "CardReaderServiceTests.TryExtractCardId_EmptyLine_ReturnsNull",
    "CardReaderServiceTests.TryExtractCardId_TwoConsecutiveCards_ReturnsFirstAndLeavesSecond",
]

# ─────────────────────────────────────────
# EXCEL GENERATION
# ─────────────────────────────────────────
def generate_excel():
    from openpyxl import Workbook
    from openpyxl.styles import (PatternFill, Font, Alignment, Border, Side,
                                  GradientFill)
    from openpyxl.utils import get_column_letter

    wb = Workbook()

    SEV_COLOR = {
        "Critical": ("FF4C4C", "FFFFFF"),
        "High":     ("FF9800", "FFFFFF"),
        "Medium":   ("FFF176", "000000"),
        "Low":      ("C8E6C9", "000000"),
    }

    thin = Side(style="thin", color="AAAAAA")
    border = Border(left=thin, right=thin, top=thin, bottom=thin)

    def header_cell(ws, row, col, text, bg="2E4057", fg="FFFFFF", bold=True, size=11):
        cell = ws.cell(row=row, column=col, value=text)
        cell.fill = PatternFill("solid", fgColor=bg)
        cell.font = Font(bold=bold, color=fg, size=size, name="微軟正黑體")
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
        cell.border = border
        return cell

    def data_cell(ws, row, col, text, bg=None, fg="000000", wrap=True, bold=False, align="left"):
        cell = ws.cell(row=row, column=col, value=text)
        if bg:
            cell.fill = PatternFill("solid", fgColor=bg)
        cell.font = Font(color=fg, size=10, name="微軟正黑體", bold=bold)
        cell.alignment = Alignment(horizontal=align, vertical="center", wrap_text=wrap)
        cell.border = border
        return cell

    # ── Sheet 1：問題清單 ──
    ws1 = wb.active
    ws1.title = "問題清單"
    ws1.sheet_view.showGridLines = False
    ws1.row_dimensions[1].height = 30

    headers = ["ID","嚴重性","類別","檔案","行號","方法 / 範圍","問題描述","建議修正","負責 Phase","狀態"]
    col_widths = [10, 10, 14, 38, 12, 22, 55, 50, 13, 8]

    for c, (h, w) in enumerate(zip(headers, col_widths), 1):
        header_cell(ws1, 1, c, h)
        ws1.column_dimensions[get_column_letter(c)].width = w

    ws1.freeze_panes = "A2"
    ws1.auto_filter.ref = f"A1:{get_column_letter(len(headers))}1"

    for r, row in enumerate(FINDINGS, 2):
        sev = row[1]
        bg, fg = SEV_COLOR.get(sev, ("FFFFFF","000000"))
        ws1.row_dimensions[r].height = 45
        for c, val in enumerate(row, 1):
            if c <= 2:
                data_cell(ws1, r, c, val, bg=bg, fg=fg, bold=(c==1), align="center")
            else:
                data_cell(ws1, r, c, val)

    # ── Sheet 2：統計摘要 ──
    ws2 = wb.create_sheet("統計摘要")
    ws2.sheet_view.showGridLines = False

    header_cell(ws2, 1, 1, "Code Review 統計摘要", bg="2E4057", size=14)
    ws2.merge_cells("A1:D1")
    ws2.row_dimensions[1].height = 35

    header_cell(ws2, 3, 1, "嚴重性")
    header_cell(ws2, 3, 2, "問題數量")
    header_cell(ws2, 3, 3, "佔比")
    header_cell(ws2, 3, 4, "說明")
    ws2.column_dimensions["A"].width = 14
    ws2.column_dimensions["B"].width = 12
    ws2.column_dimensions["C"].width = 10
    ws2.column_dimensions["D"].width = 40

    sev_counts = {"Critical":0,"High":0,"Medium":0,"Low":0}
    for f in FINDINGS:
        sev_counts[f[1]] += 1

    total = sum(sev_counts.values())
    sev_desc = {
        "Critical": "資料遺失、安全漏洞、系統崩潰風險",
        "High":     "執行緒安全、記憶體洩漏、業務邏輯缺陷",
        "Medium":   "架構問題、MVVM 違規、錯誤處理不完整",
        "Low":      "程式碼品質、命名、測試缺口",
    }
    for i, (sev, cnt) in enumerate(sev_counts.items(), 4):
        bg, fg = SEV_COLOR[sev]
        data_cell(ws2, i, 1, sev, bg=bg, fg=fg, bold=True, align="center")
        data_cell(ws2, i, 2, cnt, align="center", bold=True)
        data_cell(ws2, i, 3, f"{cnt/total*100:.1f}%", align="center")
        data_cell(ws2, i, 4, sev_desc[sev])

    ws2.row_dimensions[8].height = 10
    header_cell(ws2, 9, 1, "類別分佈", bg="2E4057")
    ws2.merge_cells("A9:D9")

    cat_counts = {}
    for f in FINDINGS:
        cat_counts[f[2]] = cat_counts.get(f[2], 0) + 1
    header_cell(ws2, 10, 1, "類別")
    header_cell(ws2, 10, 2, "數量")
    for i, (cat, cnt) in enumerate(sorted(cat_counts.items(), key=lambda x:-x[1]), 11):
        data_cell(ws2, i, 1, cat)
        data_cell(ws2, i, 2, cnt, align="center")

    completed = sum(1 for f in FINDINGS if "已修正" in f[9] or "已解決" in f[9])
    pending = sum(1 for f in FINDINGS if f[9] == "待修" or "待補" in f[9])
    no_fix = sum(1 for f in FINDINGS if f[9] in ("不修正", "暫緩"))

    data_cell(ws2, i+2, 1, f"審查日期：{REVIEW_DATE}", bold=True)
    data_cell(ws2, i+3, 1, f"發現總數：{total} 項")
    data_cell(ws2, i+4, 1, f"已修正/已解決：{completed} 項")
    data_cell(ws2, i+5, 1, f"待修/待補測試：{pending} 項")
    data_cell(ws2, i+6, 1, f"不修正/暫緩：{no_fix} 項")
    data_cell(ws2, i+7, 1, f"版本：{VERSION_TAG}")

    # ── Sheet 3：Phase 對應表 ──
    ws3 = wb.create_sheet("Phase 對應表")
    ws3.sheet_view.showGridLines = False

    header_cell(ws3, 1, 1, "Phase 修復計畫", bg="2E4057", size=14)
    ws3.merge_cells("A1:E1")
    ws3.row_dimensions[1].height = 35

    phase_headers = ["Phase","ID","嚴重性","類別","問題摘要"]
    phase_widths   = [12, 12, 12, 16, 60]
    for c, (h, w) in enumerate(zip(phase_headers, phase_widths), 1):
        header_cell(ws3, 3, c, h)
        ws3.column_dimensions[get_column_letter(c)].width = w

    phase_bg = {"Phase 1":"E3F2FD","Phase 2":"FFF8E1","Phase 3":"F3E5F5","Phase 4":"E8F5E9"}
    r = 4
    prev_phase = None
    for f in sorted(FINDINGS, key=lambda x: x[8]):
        phase = f[8]
        bg = phase_bg.get(phase, "FFFFFF")
        ws3.row_dimensions[r].height = 30
        data_cell(ws3, r, 1, phase, bg=bg, bold=True, align="center")
        data_cell(ws3, r, 2, f[0], bg=bg, align="center")
        sev_bg, sev_fg = SEV_COLOR.get(f[1], ("FFFFFF","000000"))
        data_cell(ws3, r, 3, f[1], bg=sev_bg, fg=sev_fg, align="center")
        data_cell(ws3, r, 4, f[2])
        data_cell(ws3, r, 5, f[6][:80] + ("…" if len(f[6])>80 else ""))
        r += 1

    wb.save(XLSX_PATH)
    print(f"✓ Excel 已生成：{XLSX_PATH}")


# ─────────────────────────────────────────
# WORD GENERATION
# ─────────────────────────────────────────
def generate_word():
    from docx import Document
    from docx.shared import Pt, Cm, RGBColor, Inches
    from docx.enum.text import WD_ALIGN_PARAGRAPH
    from docx.enum.table import WD_ALIGN_VERTICAL
    from docx.oxml.ns import qn
    from docx.oxml import OxmlElement
    import copy

    doc = Document()

    # ── 頁面設定 A4 ──
    section = doc.sections[0]
    section.page_width  = Cm(21)
    section.page_height = Cm(29.7)
    section.left_margin = section.right_margin = Cm(2.5)
    section.top_margin  = section.bottom_margin = Cm(2)

    FONT_CN  = "微軟正黑體"
    FONT_EN  = "Calibri"
    C_DARK   = RGBColor(0x2E, 0x40, 0x57)
    C_RED    = RGBColor(0xFF, 0x4C, 0x4C)
    C_ORANGE = RGBColor(0xFF, 0x98, 0x00)
    C_YELLOW = RGBColor(0xF5, 0x9E, 0x0E)
    C_GREEN  = RGBColor(0x4C, 0xAF, 0x50)
    C_GRAY   = RGBColor(0x66, 0x66, 0x66)

    SEV_RGB = {
        "Critical": C_RED,
        "High":     C_ORANGE,
        "Medium":   C_YELLOW,
        "Low":      C_GREEN,
    }

    def set_cell_bg(cell, hex_color):
        tc = cell._tc
        tcPr = tc.get_or_add_tcPr()
        shd = OxmlElement("w:shd")
        shd.set(qn("w:val"), "clear")
        shd.set(qn("w:color"), "auto")
        shd.set(qn("w:fill"), hex_color)
        tcPr.append(shd)

    def add_run(para, text, bold=False, size=11, color=None, font=None):
        run = para.add_run(text)
        run.bold = bold
        run.font.size = Pt(size)
        run.font.name = font or FONT_CN
        run._element.rPr.rFonts.set(qn('w:eastAsia'), font or FONT_CN)
        if color:
            run.font.color.rgb = color
        return run

    def heading(text, level=1, size=16, color=None, align=WD_ALIGN_PARAGRAPH.LEFT):
        para = doc.add_paragraph()
        para.alignment = align
        para.paragraph_format.space_before = Pt(12)
        para.paragraph_format.space_after  = Pt(6)
        add_run(para, text, bold=True, size=size, color=color or C_DARK)
        return para

    def body(text, size=10.5, indent=False, align=WD_ALIGN_PARAGRAPH.LEFT):
        para = doc.add_paragraph()
        para.alignment = align
        para.paragraph_format.space_after = Pt(4)
        if indent:
            para.paragraph_format.left_indent = Cm(0.5)
        add_run(para, text, size=size)
        return para

    def hr():
        para = doc.add_paragraph()
        pPr = para._element.get_or_add_pPr()
        pBdr = OxmlElement("w:pBdr")
        bottom = OxmlElement("w:bottom")
        bottom.set(qn("w:val"), "single")
        bottom.set(qn("w:sz"), "6")
        bottom.set(qn("w:space"), "1")
        bottom.set(qn("w:color"), "AAAAAA")
        pBdr.append(bottom)
        pPr.append(pBdr)

    # ══════════════════════════════════════
    # 封面
    # ══════════════════════════════════════
    doc.add_paragraph()
    doc.add_paragraph()
    doc.add_paragraph()

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    add_run(p, "FProductionDashBoard", bold=True, size=28, color=C_DARK)

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    add_run(p, "程式碼審查技術報告", bold=True, size=22, color=C_DARK)

    doc.add_paragraph()

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    add_run(p, "Code Review Technical Report", size=14, color=C_GRAY)

    doc.add_paragraph()
    hr()
    doc.add_paragraph()

    info_rows = [
        ("專案名稱", "FProductionDashBoard — WPF 現場生產管理系統"),
        ("技術框架", "WPF .NET 8 / C# / SQL Server / Material Design XAML"),
        ("審查日期", REVIEW_DATE),
        ("文件版本", f"{VERSION_TAG}（含二輪審查 + Phase 1-3 修復追蹤）"),
        ("文件狀態", "更新版"),
        ("審查人員", "Claude Code (AI-assisted Code Review)"),
    ]
    for label, value in info_rows:
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        add_run(p, f"{label}：", bold=True, size=11, color=C_DARK)
        add_run(p, value, size=11)

    doc.add_paragraph()
    doc.add_page_break()

    # ══════════════════════════════════════
    # 文件修訂紀錄
    # ══════════════════════════════════════
    heading("文件修訂紀錄", size=16)
    hr()

    rev_history = [
        ["v1.0", "2026-05-06", "AI-assisted Review", "初版發布：完整程式碼審查（43 項發現）", "已歸檔"],
        ["v1.1", "2026-05-13", "AI-assisted Review", "Phase 1-3 修復進度追蹤（PR #20/#21/#22/#23/#34）", "已歸檔"],
        ["v1.2", "2026-05-15", "AI-assisted Review", "PR #36 (Log 上傳) + PR #37 (B1/UI-03/IDLE-01)", "已歸檔"],
        ["v1.3", "2026-05-21", "AI-assisted Review", "二輪審查 + 18 項新發現 (P5-H1~L6) + 完整修復追蹤", "已歸檔"],
        [VERSION_TAG, REVIEW_DATE, "AI-assisted Review", "V2.5.1 發佈版本更新；PR #62 硬體設定持久化（P5-L3 已解決）", "更新版"],
    ]
    rev_tbl = doc.add_table(rows=len(rev_history)+1, cols=5)
    rev_tbl.style = "Table Grid"
    for i, h in enumerate(["版本","日期","作者","修訂內容","狀態"]):
        cell = rev_tbl.rows[0].cells[i]
        cell.text = h
        set_cell_bg(cell, "2E4057")
        for run in cell.paragraphs[0].runs:
            run.bold = True
            run.font.color.rgb = RGBColor(0xFF,0xFF,0xFF)
            run.font.name = FONT_CN
    for ri, vals in enumerate(rev_history, 1):
        row = rev_tbl.rows[ri]
        for ci, v in enumerate(vals):
            row.cells[ci].text = v
            for run in row.cells[ci].paragraphs[0].runs:
                run.font.name = FONT_CN
                run.font.size = Pt(9)

    doc.add_page_break()

    # ══════════════════════════════════════
    # 1. 專案概述
    # ══════════════════════════════════════
    heading("1. 專案概述", size=16)
    hr()

    heading("1.1 系統簡介", size=13)
    body("FProductionDashBoard 是以 WPF .NET 8 開發的現場生產管理操作系統，"
         "連接公司 SQL Server 資料庫，提供生產設備監控、巡檢記錄、人員授權、"
         "離線緩存同步等核心功能。")

    heading("1.2 技術棧", size=13)
    tech_rows = [
        ("框架 / 語言", "WPF .NET 8 / C# / XAML"),
        ("UI 套件", "Material Design XAML 5.3 / CommunityToolkit.Mvvm 8.4"),
        ("資料庫", "SQL Server（連線）/ SQLite（本地離線緩存）"),
        ("ORM", "EF Core（LocalDbContext）/ Dapper（LoginDapper）"),
        ("建置工具", "Visual Studio 2022 / ClickOnce 部署"),
        ("測試框架", "xUnit / Moq"),
    ]
    tbl = doc.add_table(rows=len(tech_rows)+1, cols=2)
    tbl.style = "Table Grid"
    for i, h in enumerate(["項目","內容"]):
        cell = tbl.rows[0].cells[i]
        cell.text = h
        set_cell_bg(cell, "2E4057")
        for run in cell.paragraphs[0].runs:
            run.bold = True; run.font.color.rgb = RGBColor(0xFF,0xFF,0xFF)
            run.font.name = FONT_CN
    for r, (k,v) in enumerate(tech_rows, 1):
        tbl.rows[r].cells[0].text = k
        tbl.rows[r].cells[1].text = v
        if r % 2 == 0:
            set_cell_bg(tbl.rows[r].cells[0], "F5F5F5")
            set_cell_bg(tbl.rows[r].cells[1], "F5F5F5")
        for ci in range(2):
            for run in tbl.rows[r].cells[ci].paragraphs[0].runs:
                run.font.name = FONT_CN

    doc.add_page_break()

    # ══════════════════════════════════════
    # 2. 審查範圍與方法
    # ══════════════════════════════════════
    heading("2. 審查範圍與方法", size=16)
    hr()

    heading("2.1 審查範圍", size=13)
    scope_items = [
        "ViewModels/（MainViewModel、DeviceCardViewModel、DeviceCardContainerViewModel 等）",
        "Services/（DataServiceV1、AuthorizationService、OfflineSyncService、CardReaderService 等）",
        "Repositories/（BaseRepository、InspectionRecordRepository、各 Record Repository）",
        "App.xaml.cs（DI 容器配置）",
        "FProductionDashBoard.Tests/（測試覆蓋率分析）",
        "appsettings.json（設定與安全性）",
    ]
    for item in scope_items:
        p = doc.add_paragraph(style="List Bullet")
        add_run(p, item, size=10.5)

    heading("2.2 審查方法", size=13)
    body("本次審查採用靜態程式碼分析（Static Code Analysis）方式，"
         "針對以下五大面向進行系統性檢視：")
    methods = [
        ("執行緒安全與並發", "ObservableCollection 執行緒、事件訂閱洩漏、DispatcherTimer 清理、競態條件"),
        ("安全性",         "連線字串保護、密碼儲存方式、例外訊息洩露、輸入驗證"),
        ("架構與設計",     "MVVM 合規性、DI Lifetime 配置、職責分離"),
        ("錯誤處理",       "空 catch block、async/await 模式、fire-and-forget 風險"),
        ("測試覆蓋",       "現有測試清單分析、關鍵業務邏輯未覆蓋區塊識別"),
    ]
    for name, desc in methods:
        p = doc.add_paragraph()
        p.paragraph_format.left_indent = Cm(0.5)
        p.paragraph_format.space_after = Pt(3)
        add_run(p, f"▪ {name}：", bold=True, size=10.5)
        add_run(p, desc, size=10.5)

    doc.add_page_break()

    # ══════════════════════════════════════
    # 3. 執行摘要
    # ══════════════════════════════════════
    heading("3. 執行摘要", size=16)
    hr()

    sev_counts = {"Critical":0,"High":0,"Medium":0,"Low":0}
    for f in FINDINGS:
        sev_counts[f[1]] += 1
    total = sum(sev_counts.values())

    completed = sum(1 for f in FINDINGS if "已修正" in f[9] or "已解決" in f[9])
    pending = sum(1 for f in FINDINGS if f[9] == "待修" or "待補" in f[9])
    no_fix = sum(1 for f in FINDINGS if f[9] in ("不修正", "暫緩"))

    body(f"本次審查累計發現 {total} 項問題（一輪 43 項 + 二輪 18 項，扣除重複/合併）。"
         f"截至本版本：已修正/已解決 {completed} 項、待修/待補測試 {pending} 項、不修正/暫緩 {no_fix} 項。"
         "以下為各嚴重等級統計：")

    sev_tbl = doc.add_table(rows=5, cols=4)
    sev_tbl.style = "Table Grid"
    for i, h in enumerate(["嚴重等級","問題數","佔比","主要問題說明"]):
        cell = sev_tbl.rows[0].cells[i]
        cell.text = h
        set_cell_bg(cell, "2E4057")
        for run in cell.paragraphs[0].runs:
            run.bold = True; run.font.color.rgb = RGBColor(0xFF,0xFF,0xFF)
            run.font.name = FONT_CN
    sev_hex = {"Critical":"FF4C4C","High":"FF9800","Medium":"FFF176","Low":"C8E6C9"}
    sev_main = {
        "Critical": "連線字串明文密碼、員工密碼未雜湊、無帳號鎖定",
        "High":     "ObservableCollection 跨執行緒、事件洩漏、資料遺失、MVVM 違規",
        "Medium":   "ConfigureAwait 缺失、空 catch、Captive Dependency、硬編碼字串",
        "Low":      "魔術數字、測試缺口、測試程式碼暴露生產環境",
    }
    for r, (sev, cnt) in enumerate(sev_counts.items(), 1):
        row = sev_tbl.rows[r]
        row.cells[0].text = sev
        row.cells[1].text = str(cnt)
        row.cells[2].text = f"{cnt/total*100:.1f}%"
        row.cells[3].text = sev_main[sev]
        set_cell_bg(row.cells[0], sev_hex[sev])
        for ci in range(4):
            for run in row.cells[ci].paragraphs[0].runs:
                run.font.name = FONT_CN
                run.font.bold = (ci == 0)

    doc.add_paragraph()
    body("⚠️ Critical 等級問題（3 項）建議最優先處理，涉及生產環境資料庫 SA 帳號密碼以明文形式"
         "存在於版本控制中，具有立即性安全風險。", size=10.5)

    doc.add_page_break()

    # ══════════════════════════════════════
    # 4. 詳細發現
    # ══════════════════════════════════════
    heading("4. 詳細發現", size=16)
    hr()

    sections_def = [
        ("4.1 安全性問題",        ["SEC-01","SEC-02","SEC-03","SEC-04","SEC-05","UI-05","P5-H2","P5-H3","P5-M6"]),
        ("4.2 執行緒安全與並發",   ["C2","C3","C3b","C4","H2","H2b","H3","M1","B1","THREAD-01","IDLE-01"]),
        ("4.3 業務邏輯缺陷",       ["B2","B3","B4","B5","B8","BUG-01","P5-H4","P5-H5","P5-M7"]),
        ("4.4 架構與 DI 設計",     ["A1","A4","A4b","CONF-01","ARCH-01","PATH-01","PATH-02"]),
        ("4.5 MVVM 合規性",        ["UI-01","UI-01b","UI-02","UI-03","UI-04","P5-M4"]),
        ("4.6 錯誤處理與測試覆蓋", ["ERR-01","ERR-02","ERR-03","ERR-04","ERR-05","TEST-01","A5","PROD-01","PROD-02","PROD-03"]),
        ("4.7 資源管理 / 事件訂閱（二輪新增）", ["P5-H1","P5-M1","P5-M2","P5-M3","P5-M5"]),
        ("4.8 程式碼品質（二輪新增）",         ["P5-L1","P5-L2","P5-L3","P5-L4","P5-L5","P5-L6"]),
    ]

    finding_map = {f[0]: f for f in FINDINGS}
    tbl_headers = ["ID","嚴重性","檔案","行號","問題描述","建議修正"]
    tbl_widths  = [1.2, 1.4, 3.2, 1.2, 7.0, 6.0]

    for sec_title, ids in sections_def:
        heading(sec_title, size=13)
        rows_data = [finding_map[i] for i in ids if i in finding_map]
        if not rows_data:
            continue

        tbl = doc.add_table(rows=len(rows_data)+1, cols=len(tbl_headers))
        tbl.style = "Table Grid"
        for ci, (h, w) in enumerate(zip(tbl_headers, tbl_widths)):
            cell = tbl.rows[0].cells[ci]
            cell.text = h
            cell.width = Inches(w / 2.54)
            set_cell_bg(cell, "2E4057")
            for run in cell.paragraphs[0].runs:
                run.bold = True; run.font.color.rgb = RGBColor(0xFF,0xFF,0xFF)
                run.font.name = FONT_CN; run.font.size = Pt(9)

        for ri, f in enumerate(rows_data, 1):
            row = tbl.rows[ri]
            vals = [f[0], f[1], f[3], f[4], f[6], f[7]]
            for ci, val in enumerate(vals):
                cell = row.cells[ci]
                cell.text = str(val)
                if ri % 2 == 0:
                    set_cell_bg(cell, "F8F9FA")
                for run in cell.paragraphs[0].runs:
                    run.font.name = FONT_CN
                    run.font.size = Pt(9)
                    if ci == 1:
                        run.font.color.rgb = SEV_RGB.get(val, RGBColor(0,0,0))
                        run.font.bold = True
                cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER

        doc.add_paragraph()

    doc.add_page_break()

    # ══════════════════════════════════════
    # 5. 修復行動計畫
    # ══════════════════════════════════════
    heading("5. 修復行動計畫", size=16)
    hr()

    phase_info = [
        ("Phase 1", "立即修復（執行緒安全 & 資料遺失）— ✅ 已完成 PR#20",
         "本 Phase 聚焦最高優先的執行緒安全問題與資料遺失風險，建立穩固的技術基礎。",
         ["C2","C3","C3b","C4","H2","H2b","H3","M1","BUG-01"]),

        ("Phase 2", "業務邏輯 & 架構修正 — ✅ 已完成 PR#21",
         "處理業務邏輯競態條件、Captive Dependency、DbContextFactory 重構。",
         ["B2","B3","B4","A4b","THREAD-01"]),

        ("Phase 3", "生產清理 + 服務封裝 — ✅ 已完成 PR#22 / #23",
         "改善架構設計、MVVM 合規性、生產環境清理。",
         ["UI-01","UI-01b","UI-04","A1","CONF-01",
          "PROD-01","PROD-02","PROD-03","ERR-05"]),

        ("Phase 4", "完整修復計畫（合併一輪遺留 + 二輪審查）— 規劃中",
         "整併一輪遺留與二輪審查 18 項新發現，建議分 4 批 PR：A 資源/異常清理、B 事件訂閱、C 業務邏輯、D 安全。",
         ["SEC-01","SEC-02","SEC-03","UI-05","UI-02","B5","B8","A5","TEST-01",
          "IDLE-01","PATH-01","PATH-02","B1","UI-03","SEC-04","A4","ERR-02",
          "P5-H1","P5-H2","P5-H3","P5-H4","P5-H5",
          "P5-M1","P5-M2","P5-M3","P5-M4","P5-M5","P5-M6","P5-M7",
          "P5-L1","P5-L2","P5-L3","P5-L4","P5-L5","P5-L6"]),
    ]

    phase_bg_hex = {"Phase 1":"E3F2FD","Phase 2":"FFF8E1","Phase 3":"F3E5F5","Phase 4":"E8F5E9"}
    for phase, subtitle, desc, ids in phase_info:
        p = doc.add_paragraph()
        p.paragraph_format.space_before = Pt(8)
        add_run(p, f"{phase}：{subtitle}", bold=True, size=12, color=C_DARK)
        body(desc)

        rows_data = [finding_map[i] for i in ids if i in finding_map]
        tbl = doc.add_table(rows=len(rows_data)+1, cols=4)
        tbl.style = "Table Grid"
        for ci, h in enumerate(["ID","嚴重性","類別","問題摘要"]):
            cell = tbl.rows[0].cells[ci]
            cell.text = h
            set_cell_bg(cell, "2E4057")
            for run in cell.paragraphs[0].runs:
                run.bold = True; run.font.color.rgb = RGBColor(0xFF,0xFF,0xFF)
                run.font.name = FONT_CN; run.font.size = Pt(9)

        for ri, f in enumerate(rows_data, 1):
            row = tbl.rows[ri]
            vals = [f[0], f[1], f[2], f[6][:70] + ("…" if len(f[6])>70 else "")]
            for ci, val in enumerate(vals):
                cell = row.cells[ci]
                cell.text = val
                set_cell_bg(cell, phase_bg_hex[phase])
                for run in cell.paragraphs[0].runs:
                    run.font.name = FONT_CN; run.font.size = Pt(9)
                    if ci == 1:
                        run.font.color.rgb = SEV_RGB.get(val, RGBColor(0,0,0))
                        run.font.bold = True
        doc.add_paragraph()

    doc.add_page_break()

    # ══════════════════════════════════════
    # 6. 附錄：現有測試清單
    # ══════════════════════════════════════
    heading("6. 附錄：現有測試清單（共 84 項）", size=16)
    hr()
    body("以下為截至審查日期，FProductionDashBoard.Tests 專案中已存在的測試方法：")
    for test in TESTS_LIST:
        p = doc.add_paragraph(style="List Bullet")
        p.paragraph_format.space_after = Pt(1)
        add_run(p, test, size=9, font=FONT_EN)

    doc.save(DOCX_PATH)
    print(f"✓ Word 已生成：{DOCX_PATH}")


# ─────────────────────────────────────────
# PDF GENERATION via Word COM
# ─────────────────────────────────────────
def generate_pdf():
    try:
        import win32com.client
        import pythoncom
        pythoncom.CoInitialize()
        word = win32com.client.Dispatch("Word.Application")
        word.Visible = False
        doc = word.Documents.Open(str(DOCX_PATH))
        doc.SaveAs(str(PDF_PATH), FileFormat=17)  # 17 = wdFormatPDF
        doc.Close()
        word.Quit()
        print(f"✓ PDF 已生成：{PDF_PATH}")
    except Exception as e:
        print(f"⚠ PDF 生成失敗（{e}），請手動從 Word 匯出 PDF")


# ─────────────────────────────────────────
# MAIN
# ─────────────────────────────────────────
if __name__ == "__main__":
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    print("開始生成文件…")
    generate_excel()
    generate_word()
    generate_pdf()
    print("\n完成！輸出目錄：", OUTPUT_DIR)
