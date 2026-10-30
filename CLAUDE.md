# CLAUDE.md

個人記帳儀表板：匯入「天天記帳」App 匯出的 CSV，存進本機 SQLite，用圖表呈現收支。單人、本機使用，沒有登入或多使用者需求。

## 技術棧

- .NET 10，Blazor Web App，**Interactive Server，全域互動**（`App.razor` 的 `<Routes @rendermode="InteractiveServer" />`）
- EF Core 10 + SQLite（`Microsoft.EntityFrameworkCore.Sqlite`）
- 圖表：[Blazor-ApexCharts](https://github.com/apexcharts/Blazor-ApexCharts)（`ApexCharts` namespace，已在 `_Imports.razor` 引入，`Program.cs` 有 `AddApexCharts()`）
- 測試：xUnit + in-memory SQLite

## 專案結構

```
ExpenseDashboard.slnx
src/
  ExpenseDashboard.Domain/   純領域邏輯：實體、CSV 解析、統計彙總。不得參考 EF Core 或 ASP.NET。
  ExpenseDashboard.Web/      Blazor UI、DbContext（Data/）、migration（Data/Migrations/）、Program.cs
tests/
  ExpenseDashboard.Tests/    xUnit，參考 Domain 與 Web
    TestData/                CSV 範例檔（複製到輸出目錄）
```

相依方向：`Web → Domain`，`Tests → Domain, Web`。Domain 保持零外部套件，好讓解析與計算邏輯能直接單元測試。

## 常用指令

```bash
dotnet build
dotnet test
dotnet run --project src/ExpenseDashboard.Web
dotnet tool restore   # 第一次使用 dotnet-ef 前
dotnet ef migrations add <Name> -p src/ExpenseDashboard.Web -o Data/Migrations
```

- 資料庫位置：`src/ExpenseDashboard.Web/App_Data/expenses.db`（連線字串 `ConnectionStrings:Expenses`），已被 `.gitignore` 排除。
- 啟動時 `Program.cs` 會自動執行 `Database.Migrate()`；改了實體或 `ExpenseDbContext` 一定要新增 migration，`Migrations_cover_the_current_model` 測試會擋下漏掉的 migration。

## 「天天記帳」CSV 格式

範例（虛構資料，格式與真實匯出相同）：`tests/ExpenseDashboard.Tests/TestData/sample-export.csv`。**不要把真實匯出檔 commit 進 repo。**

```
日期,類別,大類別,金額,貨幣,成員,帳戶,備註,收支區分,上次更新,UUID
20250104,午餐,Diet ,120,TWD,自己,現金,"便當, 加蛋",支,2025-01-04 12:30:15,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9
```

| 欄位 | 對應屬性 | 注意事項 |
|---|---|---|
| 日期 | `Date` (`DateOnly`) | `yyyyMMdd` |
| 類別 | `Category` | 小分類，例如「晚餐」「C#」 |
| 大類別 | `MajorCategory` | **可能帶尾端空白**（`Diet `），**可能為空**；要 Trim，空字串存 `null` |
| 金額 | `Amount` (`decimal`) | 一律正數，方向看「收支區分」 |
| 貨幣 | `Currency` | ISO 代碼，目前都是 TWD |
| 成員／帳戶 | `Member` / `Account` | |
| 備註 | `Note` | 自由文字，可能含逗號或引號，必須用符合 RFC 4180 的方式解析，不要直接 `Split(',')` |
| 收支區分 | `Type` | `支` → `Expense`，`收` → `Income` |
| 上次更新 | `UpdatedAt` (`DateTime`) | `yyyy-MM-dd HH:mm:ss`，本地時間（不做時區轉換） |
| UUID | `ExternalId` (`Guid`) | 唯一索引；重複匯入時用它 upsert／去重 |

- 檔案是 **UTF-8 with BOM**，第一列為標題，列依新到舊排序。
- 解析時日期、數字一律用 `CultureInfo.InvariantCulture`。

## CSV 匯入流程

1. `Import.razor`（`/import`）用 `InputFile` 讀檔（上限 20 MB），先複製到 `MemoryStream`，再以 `StreamReader`（自動去 BOM）交給解析器。
2. `Domain/Import/DailyExpenseCsvParser.Parse(TextReader)`：依**標題名稱**對應欄位（順序無關），底層用 `CsvReader`（RFC 4180）。格式錯誤的列會略過，並以 `CsvImportError(行號, 訊息)` 回報；標題缺欄位則整份不解析。行號是該列在檔案中的起始行（多行備註會被計入）。
3. `Web/Import/TransactionImporter.ImportAsync`：以 UUID upsert，回傳 `ImportSummary(Added, Updated, Unchanged)`。
   - 新 UUID → 新增；既有且「上次更新」較新 → 覆寫；否則不變。
   - 同一檔內重複 UUID 取「上次更新」最新的那筆。
   - 匯出檔中沒出現的既有資料**不刪除**（App 端刪除的紀錄不會同步）。
   - 既有資料以每批 500 個 UUID 查詢，避免 SQLite 參數上限。

## 儀表板

- `Domain/Reports/DashboardCalculator`：純函式彙總——期間摘要、每月收支（區間內每月都有一筆，空月補 0）、依大類別支出（超過 8 項併入「其他」、空大類別記為「未分類」）、支出前 10 名類別。占比分母一律是全部支出。
- `Web/Dashboard/DashboardService`：依 `DashboardFilter(Year, Currency)` 從資料庫撈資料再交給計算器。指定年份時月份固定 1–12 月；全部期間則從最早到最晚有資料的月份。**不同貨幣不混算**，貨幣選單只在資料有多種貨幣時出現。
- `Home.razor`：篩選列 → 摘要卡片 → 每月收支長條圖 → 依大類別／前 10 名橫條圖，每張圖下方都有「以表格檢視」。
- Blazor-ApexCharts 注意事項：
  - **每張圖要有自己的 `ApexChartOptions` 實例**，共用會讓其中一張畫不出來。
  - Interactive Server 下 `FormatYAxisLabel` 等 C# 委派不支援，格式化改用 options 裡的 JS 字串（`Formatter = "function (value) { ... }"`）。
  - 篩選條件改變時用 `@key` 讓圖表重建，而不是手動呼叫 `UpdateSeriesAsync`。
- 配色：沿用 dataviz 參考色盤，分類色依固定順序指派（收入 = `#2a78d6`、支出 = `#eb6834`），顏色跟著實體走、不隨排名變。色彩與表面色的 CSS token 在 `wwwroot/app.css` 的 `:root`。目前只有淺色主題。

## 開發慣例

- 在元件裡用 `IDbContextFactory<ExpenseDbContext>` 並以 `await using` 建立短命 context，**不要**直接注入 `ExpenseDbContext`（Blazor Server circuit 生命週期很長，DbContext 不是執行緒安全的）。
- 金額用 `decimal`。EF Core SQLite 會把 decimal 存成 TEXT，但 `Sum` 等彙總可以正常轉譯（有測試涵蓋）；`OrderBy` decimal 若出錯，改在用戶端排序。
- 解析、彙總等純邏輯放 Domain 並寫單元測試；牽涉資料庫的測試用 `SqliteTestDatabase`（in-memory SQLite + 實際 migration），不要用 EF InMemory provider。
- 測試命名：`Method_or_behavior_expected_result`（底線分隔的英文句子）。
- UI 文字與回覆使用者一律使用繁體中文。
- 套件版本鎖定確切版本，不用萬用字元。
