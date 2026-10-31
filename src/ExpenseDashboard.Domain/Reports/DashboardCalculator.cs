using ExpenseDashboard.Domain.Rules;
using ExpenseDashboard.Domain.Transactions;

namespace ExpenseDashboard.Domain.Reports;

/// <summary>某分類的支出合計；<see cref="Share"/> 為占該期間總支出比例（0–1）。</summary>
public sealed record CategoryTotal(string Name, decimal Amount, decimal Share);

/// <summary>年度花費摘要。<see cref="MonthCount"/> 為有分析的月份數（第一個到最後一個有資料的月份）。</summary>
public sealed record YearSummary(
    int Year,
    decimal Total,
    int Count,
    int MonthCount,
    decimal MonthlyAverage,
    MonthAnalysis? PeakMonth);

/// <summary>
/// 年度「較去年同期」：今年 1/1 到 <see cref="PeriodEnd"/> 的支出，對比去年同一段日期。
/// <see cref="Ratio"/> 為增減比例（0.1 = 多 10%），去年同期沒有支出時為 null。
/// </summary>
public sealed record YearComparison(DateOnly PeriodEnd, decimal CurrentTotal, decimal PreviousTotal, decimal? Ratio);

/// <summary>
/// 單月花費分析。<see cref="ChangeRatio"/> 為相對上月、<see cref="YearOverYearRatio"/> 為相對去年同月的
/// 增減比例（0.1 = 多 10%），比較基準沒有支出時為 null。
/// <see cref="MajorCategories"/> 是給圓餅圖用的大類別分布：只列年度前幾名的大類別，其餘併入「其他」。
/// </summary>
public sealed record MonthAnalysis(
    int Year,
    int Month,
    decimal Total,
    int Count,
    decimal DailyAverage,
    decimal? ChangeRatio,
    decimal? YearOverYearRatio,
    IReadOnlyList<CategoryTotal> MajorCategories)
{
    public string Label => $"{Year}/{Month:00}";
}

/// <summary>年度每月花費；尚未到來的月份 <see cref="Total"/> 為 null（圖上不畫），已過去但沒花費的月份為 0。</summary>
public sealed record MonthValue(int Month, decimal? Total)
{
    public string Label => $"{Month} 月";
}

public enum TrendItemKind
{
    MajorCategory,
    Category,
    /// <summary>備註關鍵字規則（<see cref="KeywordRule"/>）。</summary>
    Keyword,
}

/// <summary>
/// 可在趨勢折線圖上選擇的項目（大類別、類別或備註關鍵字規則）。<see cref="Monthly"/> 固定 12 筆（1–12 月），
/// 規則同 <see cref="MonthValue"/>：未來月份為 null，沒花費的月份為 0。
/// </summary>
public sealed record TrendItem(TrendItemKind Kind, string Name, decimal YearTotal, IReadOnlyList<decimal?> Monthly)
{
    /// <summary>關鍵字規則的資料庫 Id；其他種類為 null。</summary>
    public int? RuleId { get; init; }

    /// <summary>
    /// 穩定的識別字串，用來記住使用者的選擇（例如 <c>c:運動</c>、<c>m:Diet</c>、<c>k:3</c>）。
    /// 關鍵字規則用 Id 而不是名稱，改名後選擇仍然有效。
    /// </summary>
    public string Id => Kind switch
    {
        TrendItemKind.MajorCategory => $"m:{Name}",
        TrendItemKind.Keyword => $"k:{RuleId}",
        _ => $"c:{Name}",
    };

    public string DisplayName => Kind switch
    {
        TrendItemKind.MajorCategory => $"{Name}（大類別）",
        TrendItemKind.Keyword => $"{Name}（備註）",
        _ => Name,
    };
}

/// <summary>
/// 儀表板的彙總計算，只看支出（收支區分為「收」的紀錄一律忽略）。
/// 呼叫端負責先篩好貨幣，這裡不混算不同貨幣。
/// </summary>
public static class DashboardCalculator
{
    public const string UncategorizedName = "未分類";
    public const string OtherName = "其他";

    /// <summary>每張月卡片圓餅圖固定列出的年度前幾名大類別（其餘併入「其他」）。</summary>
    public const int FeaturedMajorCategoryCount = 5;

    /// <summary>
    /// 逐月分析 <paramref name="year"/> 年的支出，範圍是該年第一個到最後一個有支出的月份，
    /// 中間沒有支出的月份也會出現（金額為 0）。月份由舊到新。
    /// <paramref name="transactions"/> 應包含前一年的資料，用來計算「較上月」（1 月）與「較去年同月」。
    /// </summary>
    /// <param name="today">用來判斷進行中的月份：當月的日均只除到今天為止的天數。</param>
    public static IReadOnlyList<MonthAnalysis> AnalyzeMonths(IEnumerable<Transaction> transactions, int year, DateOnly today)
    {
        var expenses = Expenses(transactions).ToList();
        var byMonth = expenses
            .GroupBy(t => new DateOnly(t.Date.Year, t.Date.Month, 1))
            .ToDictionary(g => g.Key, g => g.ToList());

        var monthsInYear = byMonth.Keys.Where(m => m.Year == year).ToList();
        if (monthsInYear.Count == 0)
        {
            return [];
        }

        var featured = FeaturedMajorCategories(expenses.Where(t => t.Date.Year == year));

        var result = new List<MonthAnalysis>();
        for (var month = monthsInYear.Min(); month <= monthsInYear.Max(); month = month.AddMonths(1))
        {
            var items = byMonth.GetValueOrDefault(month) ?? [];
            var total = items.Sum(t => t.Amount);

            result.Add(new MonthAnalysis(
                month.Year,
                month.Month,
                total,
                items.Count,
                total / DaysCounted(month, today),
                Ratio(total, MonthTotal(byMonth, month.AddMonths(-1))),
                Ratio(total, MonthTotal(byMonth, month.AddYears(-1))),
                BreakdownByMajorCategory(items, featured)));
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
    /// 年度較去年同期。進行中的年度比到 <paramref name="today"/>（去年同一天；2/29 對應 2/28），
    /// 已結束的年度比整年。<paramref name="transactions"/> 需包含前一年的資料。
    /// </summary>
    public static YearComparison CompareWithPreviousYear(IEnumerable<Transaction> transactions, int year, DateOnly today)
    {
        var periodEnd = year == today.Year ? today : new DateOnly(year, 12, 31);
        var previousEnd = periodEnd.AddYears(-1);
        var expenses = Expenses(transactions).ToList();

        var current = expenses
            .Where(t => t.Date >= new DateOnly(year, 1, 1) && t.Date <= periodEnd)
            .Sum(t => t.Amount);
        var previous = expenses
            .Where(t => t.Date >= new DateOnly(year - 1, 1, 1) && t.Date <= previousEnd)
            .Sum(t => t.Amount);

        return new YearComparison(periodEnd, current, previous, Ratio(current, previous));
    }

    /// <summary><paramref name="year"/> 年 1–12 月每月的總花費。</summary>
    public static IReadOnlyList<MonthValue> MonthlyTotals(IEnumerable<Transaction> transactions, int year, DateOnly today)
    {
        var monthly = MonthlySeries(Expenses(transactions).Where(t => t.Date.Year == year), year, today);
        return monthly.Select((total, i) => new MonthValue(i + 1, total)).ToList();
    }

    /// <summary>
    /// 折線圖可選的項目，依序為：大類別、類別（皆只列該年有支出的，依年度總額由大到小）、
    /// 備註關鍵字規則（全部列出，即使該年沒有符合的支出，依年度總額再依名稱排序）。
    /// 各附 12 個月的花費。大類別只列 CSV 中真的有填大類別的；沒有大類別的類別在「類別」組已經有一模一樣的線。
    /// </summary>
    public static IReadOnlyList<TrendItem> TrendItems(
        IEnumerable<Transaction> transactions, int year, DateOnly today, IEnumerable<KeywordRule>? rules = null)
    {
        var inYear = Expenses(transactions).Where(t => t.Date.Year == year).ToList();

        IEnumerable<TrendItem> ItemsBy(TrendItemKind kind, IEnumerable<Transaction> source, Func<Transaction, string> key) => source
            .GroupBy(key, StringComparer.Ordinal)
            .Select(g => new TrendItem(kind, g.Key, g.Sum(t => t.Amount), MonthlySeries(g, year, today)))
            .Where(i => i.YearTotal > 0)
            .OrderByDescending(i => i.YearTotal)
            .ThenBy(i => i.Name, StringComparer.Ordinal);

        var keywordItems = (rules ?? [])
            .Select(rule =>
            {
                var matched = inYear.Where(t => rule.Matches(t.Note)).ToList();
                return new TrendItem(TrendItemKind.Keyword, rule.Name, matched.Sum(t => t.Amount), MonthlySeries(matched, year, today))
                {
                    RuleId = rule.Id,
                };
            })
            .OrderByDescending(i => i.YearTotal)
            .ThenBy(i => i.Name, StringComparer.Ordinal);

        return ItemsBy(TrendItemKind.MajorCategory, inYear.Where(t => t.MajorCategory is not null), MajorCategoryName)
            .Concat(ItemsBy(TrendItemKind.Category, inYear, CategoryName))
            .Concat(keywordItems)
            .ToList();
    }

    /// <summary>年度花費最多的前幾名大類別（依序），月卡片圓餅圖用它固定每個大類別的顏色。</summary>
    public static IReadOnlyList<string> FeaturedMajorCategories(IEnumerable<Transaction> transactions, int year) =>
        FeaturedMajorCategories(Expenses(transactions).Where(t => t.Date.Year == year));

    /// <summary>年度花費最多的前 <paramref name="count"/> 個類別（小分類），作為折線圖的預設選擇。</summary>
    public static IReadOnlyList<TrendItem> DefaultTrendItems(IReadOnlyList<TrendItem> items, int count = 3) =>
        items.Where(i => i.Kind == TrendItemKind.Category).Take(count).ToList();

    private static IEnumerable<Transaction> Expenses(IEnumerable<Transaction> transactions) =>
        transactions.Where(t => t.Type == TransactionType.Expense);

    /// <summary>沒有大類別的類別（例如「運動」）直接用類別名稱當大類別，不全部擠進同一個「未分類」。</summary>
    private static string MajorCategoryName(Transaction t) => t.MajorCategory ?? CategoryName(t);

    private static string CategoryName(Transaction t) => string.IsNullOrEmpty(t.Category) ? UncategorizedName : t.Category;

    private static decimal MonthTotal(Dictionary<DateOnly, List<Transaction>> byMonth, DateOnly month) =>
        byMonth.GetValueOrDefault(month)?.Sum(t => t.Amount) ?? 0;

    private static decimal? Ratio(decimal current, decimal baseline) =>
        baseline == 0 ? null : (current - baseline) / baseline;

    private static int DaysCounted(DateOnly month, DateOnly today) =>
        month.Year == today.Year && month.Month == today.Month
            ? today.Day
            : DateTime.DaysInMonth(month.Year, month.Month);

    /// <summary>今天之後的月份算「尚未到來」。</summary>
    private static bool IsFuture(int year, int month, DateOnly today) =>
        year > today.Year || (year == today.Year && month > today.Month);

    private static decimal?[] MonthlySeries(IEnumerable<Transaction> expenses, int year, DateOnly today)
    {
        var sums = new decimal[12];
        foreach (var t in expenses)
        {
            sums[t.Date.Month - 1] += t.Amount;
        }
        return Enumerable.Range(1, 12)
            .Select(m => IsFuture(year, m, today) ? (decimal?)null : sums[m - 1])
            .ToArray();
    }

    private static IReadOnlyList<string> FeaturedMajorCategories(IEnumerable<Transaction> expenses) => expenses
        .GroupBy(MajorCategoryName, StringComparer.Ordinal)
        .Select(g => (Name: g.Key, Amount: g.Sum(t => t.Amount)))
        .Where(g => g.Amount > 0)
        .OrderByDescending(g => g.Amount)
        .ThenBy(g => g.Name, StringComparer.Ordinal)
        .Take(FeaturedMajorCategoryCount)
        .Select(g => g.Name)
        .ToList();

    /// <summary>
    /// 依「大類別」分布，只列 <paramref name="featured"/> 中的大類別（依其順序，金額為 0 的略過），
    /// 其他大類別合併成最後一項「其他」。
    /// </summary>
    private static IReadOnlyList<CategoryTotal> BreakdownByMajorCategory(
        IReadOnlyCollection<Transaction> expenses, IReadOnlyList<string> featured)
    {
        var total = expenses.Sum(t => t.Amount);
        if (total == 0)
        {
            return [];
        }

        var sums = expenses
            .GroupBy(MajorCategoryName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount), StringComparer.Ordinal);

        var result = featured
            .Where(name => sums.GetValueOrDefault(name) > 0)
            .Select(name => new CategoryTotal(name, sums[name], sums[name] / total))
            .ToList();

        var rest = total - result.Sum(c => c.Amount);
        if (rest > 0)
        {
            result.Add(new CategoryTotal(OtherName, rest, rest / total));
        }
        return result;
    }
}
