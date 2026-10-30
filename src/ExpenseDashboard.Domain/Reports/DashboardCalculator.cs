using ExpenseDashboard.Domain.Transactions;

namespace ExpenseDashboard.Domain.Reports;

/// <summary>某分類的支出合計；<see cref="Share"/> 為占該期間總支出比例（0–1）。</summary>
public sealed record CategoryTotal(string Name, decimal Amount, decimal Share);

/// <summary>單筆最大支出的摘要。</summary>
public sealed record LargestExpense(DateOnly Date, string Category, decimal Amount);

/// <summary>年度花費摘要。<see cref="MonthCount"/> 為有分析的月份數（第一個到最後一個有資料的月份）。</summary>
public sealed record YearSummary(
    int Year,
    decimal Total,
    int Count,
    int MonthCount,
    decimal MonthlyAverage,
    MonthAnalysis? PeakMonth);

/// <summary>
/// 單月花費分析。<see cref="ChangeRatio"/> 為相對上月的增減比例（0.1 = 多 10%），
/// 上月沒有支出時為 null。
/// </summary>
public sealed record MonthAnalysis(
    int Year,
    int Month,
    decimal Total,
    int Count,
    decimal DailyAverage,
    decimal? ChangeRatio,
    IReadOnlyList<CategoryTotal> MajorCategories,
    LargestExpense? Largest)
{
    public string Label => $"{Year}/{Month:00}";
}

/// <summary>
/// 儀表板的彙總計算，只看支出（收支區分為「收」的紀錄一律忽略）。
/// 呼叫端負責先篩好貨幣，這裡不混算不同貨幣。
/// </summary>
public static class DashboardCalculator
{
    public const string UncategorizedName = "未分類";
    public const string OtherName = "其他";

    /// <summary>每張月卡片顯示的大類別數量（含「其他」）。</summary>
    public const int MonthCategoryCount = 4;

    /// <summary>
    /// 逐月分析 <paramref name="year"/> 年的支出，範圍是該年第一個到最後一個有支出的月份，
    /// 中間沒有支出的月份也會出現（金額為 0）。月份由舊到新。
    /// <paramref name="transactions"/> 可包含前一年 12 月的資料，用來計算 1 月的「較上月」。
    /// </summary>
    /// <param name="today">用來判斷進行中的月份：當月的日均只除到今天為止的天數。</param>
    public static IReadOnlyList<MonthAnalysis> AnalyzeMonths(IEnumerable<Transaction> transactions, int year, DateOnly today)
    {
        var byMonth = Expenses(transactions)
            .GroupBy(t => new DateOnly(t.Date.Year, t.Date.Month, 1))
            .ToDictionary(g => g.Key, g => g.ToList());

        var monthsInYear = byMonth.Keys.Where(m => m.Year == year).ToList();
        if (monthsInYear.Count == 0)
        {
            return [];
        }

        var result = new List<MonthAnalysis>();
        for (var month = monthsInYear.Min(); month <= monthsInYear.Max(); month = month.AddMonths(1))
        {
            var items = byMonth.GetValueOrDefault(month) ?? [];
            var previousTotal = byMonth.GetValueOrDefault(month.AddMonths(-1))?.Sum(t => t.Amount) ?? 0;
            var total = items.Sum(t => t.Amount);

            var largest = items
                .OrderByDescending(t => t.Amount)
                .ThenBy(t => t.Date)
                .Select(t => new LargestExpense(t.Date, t.Category, t.Amount))
                .FirstOrDefault();

            result.Add(new MonthAnalysis(
                month.Year,
                month.Month,
                total,
                items.Count,
                total / DaysCounted(month, today),
                previousTotal == 0 ? null : (total - previousTotal) / previousTotal,
                ExpenseByMajorCategory(items, MonthCategoryCount),
                largest));
        }
        return result;
    }

    /// <summary>由 <see cref="AnalyzeMonths"/> 的結果彙總出年度摘要。</summary>
    public static YearSummary SummarizeYear(int year, IReadOnlyList<MonthAnalysis> months)
    {
        var total = months.Sum(m => m.Total);
        var peak = months.Where(m => m.Total > 0).MaxBy(m => m.Total);
        return new YearSummary(
            year,
            total,
            months.Sum(m => m.Count),
            months.Count,
            months.Count == 0 ? 0 : total / months.Count,
            peak);
    }

    /// <summary>
    /// 依「大類別」彙總支出，由大到小。空的大類別歸入「未分類」；
    /// 超過 <paramref name="maxItems"/> 個時，其餘合併為最後一項「其他」。
    /// </summary>
    public static IReadOnlyList<CategoryTotal> ExpenseByMajorCategory(IEnumerable<Transaction> transactions, int maxItems = 8) =>
        ExpenseBy(transactions, t => t.MajorCategory ?? UncategorizedName, maxItems, foldRest: true);

    /// <summary>依「類別」（小分類）彙總支出，只取前 <paramref name="count"/> 名。</summary>
    public static IReadOnlyList<CategoryTotal> TopExpenseCategories(IEnumerable<Transaction> transactions, int count = 10) =>
        ExpenseBy(transactions, t => string.IsNullOrEmpty(t.Category) ? UncategorizedName : t.Category, count, foldRest: false);

    private static IEnumerable<Transaction> Expenses(IEnumerable<Transaction> transactions) =>
        transactions.Where(t => t.Type == TransactionType.Expense);

    private static int DaysCounted(DateOnly month, DateOnly today) =>
        month.Year == today.Year && month.Month == today.Month
            ? today.Day
            : DateTime.DaysInMonth(month.Year, month.Month);

    private static IReadOnlyList<CategoryTotal> ExpenseBy(
        IEnumerable<Transaction> transactions, Func<Transaction, string> key, int maxItems, bool foldRest)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxItems, 1);

        var groups = Expenses(transactions)
            .GroupBy(key, StringComparer.Ordinal)
            .Select(g => (Name: g.Key, Amount: g.Sum(t => t.Amount)))
            .Where(g => g.Amount > 0)
            .OrderByDescending(g => g.Amount)
            .ThenBy(g => g.Name, StringComparer.Ordinal)
            .ToList();

        var total = groups.Sum(g => g.Amount);
        if (total == 0)
        {
            return [];
        }

        if (foldRest && groups.Count > maxItems)
        {
            var kept = groups.Take(maxItems - 1).ToList();
            kept.Add((OtherName, groups.Skip(maxItems - 1).Sum(g => g.Amount)));
            groups = kept;
        }
        else
        {
            groups = groups.Take(maxItems).ToList();
        }

        return groups.Select(g => new CategoryTotal(g.Name, g.Amount, g.Amount / total)).ToList();
    }
}
