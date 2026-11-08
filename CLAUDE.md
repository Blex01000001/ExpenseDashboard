# CLAUDE.md

個人記帳儀表板：匯入「天天記帳」App 匯出的 CSV → 本機 SQLite → Blazor 圖表。單人本機使用，無登入。.NET 10 Blazor Web App（**Interactive Server、全域互動**）＋ EF Core SQLite ＋ Blazor-ApexCharts。函式與元件細節以程式碼與檔頭註解為準。

## 檔案地圖

| 檔案 | 負責 | 常見修改 |
|---|---|---|
| `src/ExpenseDashboard.Domain/Import/DailyExpenseCsvParser.cs` | CSV → `Transaction`，依標題名稱對應欄位，逐列回報錯誤 | App 改欄名 → 加 `ColumnAliases` |
| `src/ExpenseDashboard.Domain/Import/CsvReader.cs` | RFC 4180 讀取（引號、欄內逗號／換行） | 很少 |
| `src/ExpenseDashboard.Domain/Transactions/Transaction.cs` | 實體；`HasSameContent`、`ChangesFrom`（匯入比對與改動明細） | 新增欄位 |
| `src/ExpenseDashboard.Domain/Reports/DashboardCalculator.cs` | 儀表板所有彙總（月卡、年度、較去年同期、折線項目） | 新統計數字 |
| `src/ExpenseDashboard.Domain/Reports/TagCalculator.cs` | 標籤拆分與彙總 | 標籤規則 |
| `src/ExpenseDashboard.Domain/Rules/KeywordRule.cs` | 備註關鍵字比對（字邊界、`*`） | 比對規則 |
| `src/ExpenseDashboard.Web/Import/TransactionImporter.cs` | upsert、改動明細、同步刪除（`FindMissingAsync`／`DeleteAsync`） | 匯入規則 |
| `src/ExpenseDashboard.Web/Dashboard/DashboardService.cs` | 撈資料（含前一整年）→ `DashboardCalculator` | 新增儀表板資料 |
| `src/ExpenseDashboard.Web/Rules/KeywordRuleService.cs`、`Tags/TagService.cs` | 關鍵字規則 CRUD／符合紀錄；標籤彙總 | — |
| `src/ExpenseDashboard.Web/Data/ExpenseDbContext.cs`、`Data/Migrations/` | EF 模型與 migration | 改實體後加 migration |
| `src/ExpenseDashboard.Web/Program.cs` | DI 註冊、啟動時 `Database.Migrate()` | 新服務要在這註冊 |
| `Components/Pages/Home.razor` | 儀表板版面 | 版面調整 |
| `Components/Pages/MonthlyTrendCard.razor`、`MonthCard.razor`、`ChangeBadge.razor`；`Components/ChartTheme.cs` | 每月花費圖、月卡片、▲▼ 增減；圖表色票與格式化 | 圖表 |
| `Components/Pages/Import.razor`、`KeywordRules.razor`、`Tags.razor`；`YearCurrencyFilter.razor` | 匯入頁、`/keywords`、`/tags`；標題列年度／貨幣選單（儀表板與標籤頁共用） | — |
| `Components/Layout/MainLayout.razor`(+`.css`) | 左上懸浮導覽、錯誤提示列、`main` 讓位 | 新頁面要加導覽按鈕 |
| `src/ExpenseDashboard.Web/wwwroot/app.css`；`Components/**/X.razor.css` | 全站：token（`:root`）、基本元素、卡片、按鈕、三頁共用的規則／明細表格；元件專屬樣式放各自的 scoped CSS | 樣式子元件要用 `::deep` |
| `tests/ExpenseDashboard.Tests/` | `Import/`、`Reports/`、`Rules/`、`Tags/`、`Dashboard/`：單元＋SQLite；`Components/`：bUnit | 每個改動都要補 |
| `tests/.../Data/SqliteTestDatabase.cs`、`FixedTimeProvider.cs`、`TestTransactions.cs`、`Components/ComponentTestContext.cs` | 測試用 DB、固定「今天」、測試交易（只指定在意的欄位）、bUnit 共用環境 | 新欄位加預設值 |
| `tests/.../TestData/sample-export*.csv` | 新舊兩版匯出格式範例（虛構） | 新格式加一份 |
| `scripts/check-ui.cjs`、`scripts/demo-data.py` | 截圖檢查、虛構資料產生器 | 新頁面加進 `pages` |
| `.claude/hooks/session-start.sh`、`.claude/settings.json` | 雲端開機裝 .NET 10 SDK＋restore；常用指令免詢問白名單 | 新常用指令加白名單 |
| `.github/workflows/ci.yml` | 每次 push／PR 跑 `dotnet test`（Release） | — |

（`Components/` = `src/ExpenseDashboard.Web/Components/`）

## 修改後的驗證

```bash
dotnet test                      # 每次 commit 前必跑。首次約 30 秒（含編譯），之後約 5–7 秒
dotnet test --filter "FullyQualifiedName~Components"   # 只跑 bUnit 元件測試，約 5 秒
dotnet build                     # 只改 .razor/.css 時快速確認，約 3 秒
```

- **測試全過才 commit；不可為了通過而跳過、刪除或弱化測試。**
- 改了實體或 `ExpenseDbContext`：先加 migration，`Migrations_cover_the_current_model` 會擋漏掉的。
  ```bash
  dotnet tool restore   # 第一次用 dotnet-ef 前，約 5 秒
  dotnet ef migrations add <Name> -p src/ExpenseDashboard.Web -o Data/Migrations
  ```
- 改畫面：除了 bUnit，還要跑截圖檢查（桌面 1280、手機 375，不可出現水平捲動）；ApexCharts 在 bUnit 中是 stub，圖形只能靠截圖驗證。
  ```bash
  node scripts/check-ui.cjs              # 暫存 DB + 虛構資料，4 頁 × 2 尺寸截圖到 artifacts/ui/，檢查水平捲動與瀏覽器錯誤，約 30 秒
  node scripts/check-ui.cjs --readme     # 另外更新 README 截圖 docs/images/dashboard.png
  python3 scripts/demo-data.py out.csv   # 只產生虛構匯出檔（固定種子）
  ```

## 常見任務

- **App 匯出改欄名**：`ColumnAliases` 加「別名 → 正式名稱」；`TestData/` 加範例；`DailyExpenseCsvParserTests` 加測試。錯誤訊息一律用正式名稱。
- **新增儀表板數字**：純計算寫在 `DashboardCalculator`（+`DashboardCalculatorTests`）→ 放進 `DashboardData` → `Home.razor`；需要前一年資料的話 `DashboardService` 已經撈了。
- **新增頁面**：`Components/Pages/X.razor` + `MainLayout.razor` 導覽按鈕（標籤短，手機只顯示圖示）+ bUnit 測試；頁面名稱和測試命名空間同名時用 using 別名（見 `ImportPageTests`）。
- **新增服務**：放 `Web/<功能>/`，`Program.cs` 註冊 Scoped，並在 `ComponentTestContext` 也註冊。
- **新增實體欄位**：`Transaction` → `HasSameContent`／`ChangesFrom`／`TransactionImporter.CopyValues` 三處都要加 → migration。

## 架構決策（不要改回去）

- **元件用 `IDbContextFactory` + `await using`，不直接注入 `ExpenseDbContext`**：Blazor Server circuit 很長，DbContext 非執行緒安全。
- **Domain 不參考 EF Core／ASP.NET、零外部套件**：解析與計算才能直接單元測試。
- **測試用 in-memory SQLite + 真的 migration，不用 EF InMemory provider**：InMemory 不會發現 SQLite 轉譯與 migration 問題。
- **金額用 `decimal`**：SQLite 存成 TEXT，`Sum` 可轉譯（有測試）；`OrderBy` decimal 若出錯改在用戶端排序。
- **每張 ApexChart 各自一個 `ApexChartOptions` 實例**：共用時其中一張畫不出來。
- **圖表格式化用 options 裡的 JS 字串（`Formatter = "function ..."`）**：Interactive Server 不支援 `FormatYAxisLabel` 等 C# 委派。
- **篩選改變用 `@key` 重建圖表**，不用 `UpdateSeriesAsync`。
- **畫面上的金額／筆數／百分比／日期一律用 `Components/Display.cs`**（InvariantCulture；百分比不用 `"P0"`，伺服器文化會在數字和 % 之間插空白）。類別不能叫 `Format`：和 `ApexCharts.Format` 撞名。
- **只看支出**：使用者沒有收入；解析器仍接受「收」，但所有彙總忽略收入。
- **沒有大類別的類別直接當大類別**（不併成「未分類」）：使用者指定。
- **甜甜圈只列年度前 5 名大類別、其餘併「其他」**：讓每張月卡片同一大類別同色。
- **關鍵字規則與標籤只進折線／彙總頁，不進甜甜圈**：一筆會同時算進多個項目，加總不等於總額。
- **匯入覆寫規則**：「上次更新」較新、或相同但內容不同 → 覆寫；較舊 → 不動（防舊檔蓋新資料）。使用者已實測 App 改紀錄（含改類別）會更新該時間。
- **同步刪除**：預設關閉；只看匯出檔日期範圍內；先預覽、確認才刪、只刪預覽列出的 Id；檔案有解析錯誤就不檢查（未知 UUID 會被誤判為已刪除）。
- **英數關鍵字只比對整個字**（`BA` 不中「linebank」），`*` 放寬一側：使用者遇過誤抓。

## 設計規範

- **只有暗色主題**；顏色一律用 `app.css` `:root` 的 token。圖表色與 JS 格式化字串集中在 `Components/ChartTheme.cs`，已用 dataviz 驗證工具驗過對 `#1a1a19` 的對比與色盲區分，**換色前要重新驗證**。
- 文字不用系列色上色；例外：增減 ▲ 紅（`--change-up`）▼ 綠（`--change-down`），只上色三角形與百分比，且一定有符號。
- 破壞性按鈕用 `.btn-danger`（紅底白字）。錯誤提示（錯誤列、重連視窗、錯誤頁、404）一律中文暗色。
- 響應式斷點 720px：導覽只剩圖示、表格改成卡片式（用 `>` 子代選擇器，避免套到巢狀表格）。
- **UI 文字與回覆使用者一律使用繁體中文。**

## 對外格式與計算定義（修改前須先與使用者確認）

- **CSV 輸入**：UTF-8 with BOM、首列標題、新到舊。舊版有「貨幣」欄；新版改名「幣別」並多「標籤」欄，兩種都要支援。「大類別」可能帶尾端空白或為空。「備註」可含逗號／引號／換行。UUID 是去重鍵；日期 `yyyyMMdd`、上次更新 `yyyy-MM-dd HH:mm:ss` 本地時間；一律 `InvariantCulture`。
- **儀表板版面順序、各項統計的定義**（月份範圍、日均、較上月／去年同月／去年同期、前 5 名）：都是使用者逐項決定過的，定義寫在 `DashboardCalculator` 的註解。
- 使用者目前不用「標籤」欄，分類靠備註＋關鍵字規則。

## 資料保存

| 項目 | 位置 | 失敗時 |
|---|---|---|
| 交易、關鍵字規則 | `src/ExpenseDashboard.Web/App_Data/expenses.db`（`ConnectionStrings:Expenses`），`.gitignore` 排除 | 啟動自動 migrate |
| 折線項目選擇 | 瀏覽器 `localStorage` `expense-dashboard.trend-items` | 讀寫失敗 → 預設項目 |

- **不要把真實匯出檔 commit 進 repo**；測試資料一律虛構。

## 環境陷阱

- 需要 .NET 10 SDK。使用者用 **Visual Studio 2026**（VS 2022 不支援 .NET 10）；要開 `ExpenseDashboard.slnx`（不是資料夾檢視），用 `https`／`http` 啟動設定檔。直接執行 `bin/` 的 exe 會是 Production、靜態資源載不到、畫面沒有互動。
- 使用者本機路徑含 `#`（`E:\Code\C#\...`）。
- 雲端容器：`dotnet` 用 apt 的 `dotnet-sdk-10.0` 安裝（`dot.net` 下載被擋，SessionStart hook 會自動裝），NuGet 可連。
- 停測試伺服器用 `fuser -k 5199/tcp`；**`pkill -f`／`pgrep -f` 會連自己的 shell 一起砍**。
- CSS 註解裡不能出現 `*/`（例如路徑 `Components/**/`），會提早結束註解、整份樣式失效。
- Playwright 對同一個檔案重複 `setInputFiles` 不會觸發 change，重複匯入要開新頁面。
- **套件版本鎖定確切版本，不用萬用字元。**

## 工作流程規則

- 開發分支 `claude/wizardly-curie-3v1j5b`；**只有使用者說「合併到 main」才合併**（fast-forward 後 push）。
- 需求有需要使用者決定的地方，先列出選項與建議，等回覆再改檔；使用者說「先討論」時不改檔案。

## Git 規則
- 每完成一個獨立的修改就 commit
- commit 訊息第一行簡述改了什麼，需要時空一行補充原因
- commit 訊息用英文
- commit 訊息只描述本次修改，不要附上待辦清單
- 需要了解過去的修改時，用 git log 查詢

## PROGRESS.md
- 記錄已知限制與待辦，需要時再讀，不必每次 commit 都讀
- 只在以下情況更新：完成待辦項目、發現新的已知限制或問題
- 完成的待辦直接刪除，不要搬到「已完成」
- 發現限制或問題解除時直接刪除

## 完成任務後的回覆
簡短列出：
1. **本次修改**：做了什麼
2. **尚待改善項目／問題、已知問題**：還沒做完的、發現但未處理的問題、已知限制（沒有就省略）
3. **建議改善方向或功能**：之後可以考慮的改進或新功能，或是可以提高CLAUDE CODE效率的建議（沒有就省略）
