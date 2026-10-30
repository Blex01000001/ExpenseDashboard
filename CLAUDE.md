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

## 開發慣例

- 在元件裡用 `IDbContextFactory<ExpenseDbContext>` 並以 `await using` 建立短命 context，**不要**直接注入 `ExpenseDbContext`（Blazor Server circuit 生命週期很長，DbContext 不是執行緒安全的）。
- 金額用 `decimal`。EF Core SQLite 會把 decimal 存成 TEXT，但 `Sum` 等彙總可以正常轉譯（有測試涵蓋）；`OrderBy` decimal 若出錯，改在用戶端排序。
- 解析、彙總等純邏輯放 Domain 並寫單元測試；牽涉資料庫的測試用 `SqliteTestDatabase`（in-memory SQLite + 實際 migration），不要用 EF InMemory provider。
- 測試命名：`Method_or_behavior_expected_result`（底線分隔的英文句子）。
- UI 文字使用繁體中文。
- 套件版本鎖定確切版本，不用萬用字元。
