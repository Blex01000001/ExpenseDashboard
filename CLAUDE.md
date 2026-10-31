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
  ExpenseDashboard.Domain/   純領域邏輯：實體、CSV 解析、統計彙總、備註關鍵字規則。不得參考 EF Core 或 ASP.NET。
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

App 不同版本的匯出格式不同，兩種都要支援（範例皆為虛構資料，**不要把真實匯出檔 commit 進 repo**）：

- 舊版：`TestData/sample-export.csv`

```
日期,類別,大類別,金額,貨幣,成員,帳戶,備註,收支區分,上次更新,UUID
20250104,午餐,Diet ,120,TWD,自己,現金,"便當, 加蛋",支,2025-01-04 12:30:15,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9
```

- 新版：`TestData/sample-export-v2.csv`——「貨幣」改名「幣別」，並多了「標籤」欄

```
日期,類別,大類別,金額,幣別,成員,帳戶,標籤,備註,收支區分,上次更新,UUID
20250210,遊戲,,300,TWD,自己,現金,休閒,"Steam, 特價",支,2025-02-10 21:05:11,5E6F7081-92A3-44B5-C6D7-E8F901122334
```

欄名別名寫在 `DailyExpenseCsvParser.ColumnAliases`（別名 → 正式名稱）；遇到新的改名就加在那裡，錯誤訊息一律用正式名稱。

| 欄位 | 對應屬性 | 注意事項 |
|---|---|---|
| 日期 | `Date` (`DateOnly`) | `yyyyMMdd` |
| 類別 | `Category` | 小分類，例如「晚餐」「C#」 |
| 大類別 | `MajorCategory` | **可能帶尾端空白**（`Diet `），**可能為空**；要 Trim，空字串存 `null` |
| 金額 | `Amount` (`decimal`) | 一律正數，方向看「收支區分」 |
| 貨幣（新版：幣別） | `Currency` | ISO 代碼，目前都是 TWD |
| 成員／帳戶 | `Member` / `Account` | |
| 備註 | `Note` | 自由文字，可能含逗號或引號，必須用符合 RFC 4180 的方式解析，不要直接 `Split(',')` |
| 標籤（選填，新版才有） | `Tags` | 原樣保存（可能含逗號），空字串存 `null`；舊版匯出沒有這欄 |
| 收支區分 | `Type` | `支` → `Expense`，`收` → `Income` |
| 上次更新 | `UpdatedAt` (`DateTime`) | `yyyy-MM-dd HH:mm:ss`，本地時間（不做時區轉換） |
| UUID | `ExternalId` (`Guid`) | 唯一索引；重複匯入時用它 upsert／去重 |

- 檔案是 **UTF-8 with BOM**，第一列為標題，列依新到舊排序。
- 解析時日期、數字一律用 `CultureInfo.InvariantCulture`。

## CSV 匯入流程

1. `Import.razor`（`/import`）用 `InputFile` 讀檔（上限 20 MB），先複製到 `MemoryStream`，再以 `StreamReader`（自動去 BOM）交給解析器。
2. `Domain/Import/DailyExpenseCsvParser.Parse(TextReader)`：依**標題名稱**對應欄位（順序無關），底層用 `CsvReader`（RFC 4180）。格式錯誤的列會略過，並以 `CsvImportError(行號, 訊息)` 回報；標題缺欄位則整份不解析。行號是該列在檔案中的起始行（多行備註會被計入）。
3. `Web/Import/TransactionImporter.ImportAsync`：以 UUID upsert，回傳 `ImportSummary(Added, Updated, Unchanged)`。
   - 新 UUID → 新增；既有且「上次更新」**較新** → 覆寫；「上次更新」**相同但內容不同**（`Transaction.HasSameContent`）→ 也覆寫；較舊 → 不動（避免舊匯出檔蓋回新資料）。
   - 使用者已實測：在 App 修改舊紀錄（含改類別）時「上次更新」會跟著變。
   - 同一檔內重複 UUID 取「上次更新」最新的那筆。
   - 既有資料以每批 500 個 UUID 查詢，避免 SQLite 參數上限。
4. **同步刪除**（匯入頁勾選「同步刪除」才做，預設關閉）：
   - `FindMissingAsync`：只找匯出檔**日期範圍內**（檔案最早到最晚的日期，含兩端）、資料庫有但檔案沒有的紀錄；範圍外一律不碰，只匯出部分期間也不會誤刪。
   - 先列出預覽（最多顯示 100 筆），使用者按「確定刪除」才呼叫 `DeleteAsync`，且只刪預覽列出的那些 Id。
   - 檔案有任何無法解析的列時**不做刪除檢查**（那些列的 UUID 未知，會被誤判成已刪除）。

## 儀表板

使用者的資料**只有支出、不會有收入**。儀表板只看支出；解析器仍接受「收」，但收入紀錄在所有彙總中都會被忽略。

**沒有大類別的類別**（CSV 大類別欄為空，例如「運動」「ゲーム」）在大類別相關的彙總（圓餅圖、年度前 5 名）中，直接用類別名稱當大類別，不併成單一的「未分類」。`UncategorizedName` 只用在連類別都是空的罕見情況。

- `Domain/Reports/DashboardCalculator`（純函式）：
  - `AnalyzeMonths(transactions, year, today)`：月卡片資料，範圍是該年**第一個到最後一個有支出的月份**（中間空月補 0）。每月有總額、筆數、日均、較上月、較去年同月（基準為 0 → `null`），以及圓餅圖用的大類別分布：只列**年度前 5 名大類別**（依年度排名順序），其餘併入「其他」，讓同一個大類別在每張卡片顏色一致。進行中的月份（`today` 所在月）日均只除到今天。
  - `SummarizeYear`：由月份結果彙總年度總額、筆數、月平均（除以統計月份數）、最高月份。
  - `MonthlyTotals`：每月花費長條圖，固定 1–12 月；`today` 之後的月份為 `null`（不畫），已過去沒花費的月份為 0。
  - `TrendItems`：折線圖可選項目，依序為大類別（只列 CSV 真的有填大類別的，避免和類別組重複）、類別、備註關鍵字規則（全部列出，即使今年沒有符合）。`TrendItem.Id` 為 `m:名稱`／`c:名稱`／`k:規則Id`（規則用 Id，改名後選擇仍有效），各附 12 個月金額，規則同上。`DefaultTrendItems` 取年度前 3 名類別。
  - `FeaturedMajorCategories`：年度前 5 名大類別，月卡片圓餅圖的固定配色依據。
- `Web/Dashboard/DashboardService`：依 `DashboardFilter(Year, Currency)` 撈資料，**多撈前一整年**（較上月、較去年同月）；`today` 取自注入的 `TimeProvider`（測試用 `FixedTimeProvider`）。**不同貨幣不混算**，貨幣選單只在資料有多種貨幣時出現。
- 版面由上而下：標題列（「記帳儀表板」＋年度選單，同一行）→ 年度花費卡 → `MonthlyTrendCard.razor`（每月花費長條＋折線）→ `MonthCard.razor`（一個月一張，新到舊）。
  - 導覽是 `MainLayout` 左上角的固定懸浮按鈕（儀表板、匯入 CSV、備註關鍵字）；窄螢幕（≤720px）只顯示圖示，`main` 會讓出左側空間。
  - 折線圖：長條＝總花費（左軸）、折線＝所選項目（右軸，所有折線共用）。最多 5 項，顏色與線型綁在「欄位」上（移除一項不會讓其他項換色）；選擇存在瀏覽器 `localStorage`（`expense-dashboard.trend-items`），讀寫失敗就用預設。圖表圖例關閉，由上方的項目標籤代替。
  - 月卡片：總額、較上月、較去年同月（`ChangeBadge.razor`）、日均、大類別甜甜圈圖（中間顯示總額）。
- **備註關鍵字規則**（`Domain/Rules/KeywordRule`，存在 `KeywordRules` 資料表，`Keywords` 為 EF primitive collection）：
  - 備註符合任一關鍵字即算，不分大小寫；只比對備註，不比對標籤。
  - **英數字關鍵字只比對整個字**：關鍵字開頭／結尾是英文字母或數字時，該側前後不能緊接其他英數字（`BA` 符合「BA 課金」「BA課金」，不符合「linebank」）。中文沒有字邊界，照樣「包含」比對。關鍵字前後加 `*` 取消該側限制（`*GPT` 符合「ChatGPT」）。實作在 `KeywordRule.FindMatch`，回傳符合位置供畫面標示。
  - 只在統計時比對、不寫回交易，修改規則立刻套用到全部資料。
  - **只用在折線圖**，不進圓餅圖：同一筆支出會同時算在原本的類別和關鍵字項目，加總不等於總額。
  - 設定頁 `/keywords`（`KeywordRules.razor` + `Web/Rules/KeywordRuleService`）：規則列表在上、新增／編輯表單在下（按「編輯」會把焦點移到表單）。名稱唯一，列表顯示全部年份、全部貨幣的符合筆數與金額；點一列展開符合的紀錄（`GetMatchesAsync`，日期、金額、類別、備註並標示符合處，最多顯示 200 筆）。刪除按鈕為紅底白字（`.btn-danger`，`--danger-solid`）。
- Blazor-ApexCharts 注意事項：
  - **每張圖要有自己的 `ApexChartOptions` 實例**，共用會讓其中一張畫不出來。
  - Interactive Server 下 `FormatYAxisLabel` 等 C# 委派不支援，格式化改用 options 裡的 JS 字串（`Formatter = "function (value) { ... }"`）。
  - 篩選條件改變時用 `@key` 讓圖表重建，而不是手動呼叫 `UpdateSeriesAsync`。
  - 暗色：`Theme.Mode = Mode.Dark`、`Chart.Background = "transparent"`，色值與 CSS token 保持一致。
- 百分比不要用 `"P0"` 格式（伺服器文化會在數字和 % 之間插空白），用 `$"{ratio * 100:0}%"`。

## 主題與配色

- **只有暗色主題**。色彩 token 在 `wwwroot/app.css` 的 `:root`，取自 dataviz 參考色盤暗色版本：頁面 `#0d0d0d`、卡片／圖表表面 `#1a1a19`、主要文字 `#f0efec`、次要 `#c3c2b7`、muted `#898781`、格線 `#2c2c2a`。
- 支出（長條）色 `#d95926`（參考色盤 slot 2 橘的暗色階）。
- 折線依序用 `#3987e5`、`#199e70`、`#c98500`、`#d55181`、`#008300`；超過 3 條時色盲難以分辨（已用驗證工具確認），所以每個欄位另有固定虛線樣式當第二種編碼。
- 甜甜圈圖：年度前 5 名大類別依序用 `#3987e5`、`#d95926`、`#199e70`、`#c98500`、`#d55181`，「其他」用 `#898781`。
- 增減（使用者要求上色）：▲ 花得比較多＝`--change-up` `#e66767`、▼ 花得比較少＝`--change-down` `#0ca30c`；只有三角形與百分比上色，標籤文字維持文字色，且一定有 ▲▼ 符號，不只靠顏色。
- 其他文字一律用文字 token，不用系列色上色。

## 開發慣例

- 在元件裡用 `IDbContextFactory<ExpenseDbContext>` 並以 `await using` 建立短命 context，**不要**直接注入 `ExpenseDbContext`（Blazor Server circuit 生命週期很長，DbContext 不是執行緒安全的）。
- 金額用 `decimal`。EF Core SQLite 會把 decimal 存成 TEXT，但 `Sum` 等彙總可以正常轉譯（有測試涵蓋）；`OrderBy` decimal 若出錯，改在用戶端排序。
- 解析、彙總等純邏輯放 Domain 並寫單元測試；牽涉資料庫的測試用 `SqliteTestDatabase`（in-memory SQLite + 實際 migration），不要用 EF InMemory provider。
- 測試命名：`Method_or_behavior_expected_result`（底線分隔的英文句子）。
- UI 文字與回覆使用者一律使用繁體中文。
- 套件版本鎖定確切版本，不用萬用字元。

## Commit 規則

每次 commit 都要：

1. 更新根目錄的 `PROGRESS.md`，三個段落保持為目前的完整狀態（不是只寫這次的差異）：
   - **已改善**：已完成、可用的功能與修正。
   - **尚待改善**：已知的缺陷、限制、缺少的測試。
   - **建議改善方向或功能**：之後可以做的方向。
2. commit 訊息在摘要說明之後，同樣列出這三個段落（以繁體中文，內容針對這次 commit：這次改善了什麼、這次留下或發現的待改善項目、建議的下一步）。

格式：

```
<英文一行摘要>

<變更說明>

已改善：
- ...

尚待改善：
- ...

建議改善方向或功能：
- ...
```
