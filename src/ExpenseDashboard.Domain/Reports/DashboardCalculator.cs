using ExpenseDashboard.Domain.Transactions;

namespace ExpenseDashboard.Domain.Reports;

public sealed record PeriodSummary(decimal Income, decimal Expense, int Count)
{
    public decimal Net => Income - Expense;
}

public sealed record MonthlyTotal(int Year, int Month, decimal Income, decimal Expense)
{
    public string Label => $"{Year}/{Month:00}";
    public decimal Net => Income - Expense;
}

/// <summary>某分類的支出合計；<see cref="Share"/> 為占總支出比例（0–1）。</summary>
public sealed record CategoryTotal(string Name, decimal Amount, decimal Share);

/// <summary>
/// 儀表板的彙總計算。呼叫端負責先篩好期間與貨幣，這裡不混算不同貨幣。
/// </summary>
public static class DashboardCalculator
{
    public const string UncategorizedName = "未分類";
    public const string OtherName = "其他";

    public static PeriodSummary Summarize(IEnumerable<Transaction> transactions)
    {
        decimal income = 0, expense = 0;
        var count = 0;
        foreach (var t in transactions)
        {
            if (t.Type == TransactionType.Income)
            {
                income += t.Amount;
            }
            else
            {
                expense += t.Amount;
            }
            count++;
        }
        return new PeriodSummary(income, expense, count);
    }

    /// <summary>
    /// 依月份彙總收支，<paramref name="from"/> 到 <paramref name="to"/> 之間每個月都有一筆（沒資料的月份為 0），
    /// 區間外的交易忽略。
    /// </summary>
    public static IReadOnlyList<MonthlyTotal> MonthlyTotals(IEnumerable<Transaction> transactions, DateOnly from, DateOnly to)
    {
        var first = new DateOnly(from.Year, from.Month, 1);
        var last = new DateOnly(to.Year, to.Month, 1);
        if (first > last)
        {
            return [];
        }

        var totals = transactions
            .Where(t => t.Date >= from && t.Date <= to)
            .GroupBy(t => (t.Date.Year, t.Date.Month))
            .ToDictionary(
                g => g.Key,
                g => (Income: g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                      Expense: g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)));

        var result = new List<MonthlyTotal>();
        for (var month = first; month <= last; month = month.AddMonths(1))
        {
            var (income, expense) = totals.GetValueOrDefault((month.Year, month.Month));
            result.Add(new MonthlyTotal(month.Year, month.Month, income, expense));
        }
        return result;
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

    private static IReadOnlyList<CategoryTotal> ExpenseBy(
        IEnumerable<Transaction> transactions, Func<Transaction, string> key, int maxItems, bool foldRest)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxItems, 1);

        var groups = transactions
            .Where(t => t.Type == TransactionType.Expense)
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
