using ExpenseDashboard.Domain.Transactions;

namespace ExpenseDashboard.Domain.Reports;

/// <summary>
/// 單一標籤的年度彙總。<see cref="Share"/> 為占該年度全部支出的比例（0–1）；
/// <see cref="Monthly"/> 固定 12 筆，規則同 <see cref="MonthValue"/>（未來月份 null、沒花費的月份 0）。
/// </summary>
public sealed record TagSummary(string Name, decimal Total, int Count, decimal Share, IReadOnlyList<decimal?> Monthly);

/// <summary>標籤彙總頁的整體結果。</summary>
/// <param name="YearTotal">該年度全部支出（含沒有標籤的）。</param>
/// <param name="TaggedCount">有任何標籤的支出筆數。</param>
/// <param name="TotalCount">該年度全部支出筆數。</param>
public sealed record TagReport(IReadOnlyList<TagSummary> Tags, decimal YearTotal, int TaggedCount, int TotalCount);

/// <summary>
/// 依 CSV「標籤」欄彙總支出。一筆紀錄有多個標籤時，會算進每一個標籤，所以各標籤加總可能大於年度總額。
/// </summary>
public static class TagCalculator
{
    // 不拆空白：標籤名稱本身可能含空白（例如「日本 旅行」）。
    private static readonly char[] Separators = [',', '，', '、', ';', '；', '\n', '\r'];

    /// <summary>拆出一筆紀錄的標籤：去頭尾空白、略過空的、不分大小寫去重，保留原本順序與寫法。</summary>
    public static IReadOnlyList<string> SplitTags(string? tags) =>
        (tags ?? string.Empty)
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static bool HasTag(Transaction transaction, string tag) =>
        SplitTags(transaction.Tags).Contains(tag, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="year"/> 年各標籤的支出彙總，依金額由大到小（同金額依名稱）。
    /// 標籤名稱不分大小寫視為同一個，顯示用第一次出現的寫法。
    /// </summary>
    public static TagReport Summarize(IEnumerable<Transaction> transactions, int year, DateOnly today)
    {
        var expenses = transactions
            .Where(t => t.Type == TransactionType.Expense && t.Date.Year == year)
            .ToList();
        var yearTotal = expenses.Sum(t => t.Amount);

        var tagged = expenses
            .SelectMany(t => SplitTags(t.Tags).Select(tag => (Tag: tag, Transaction: t)))
            .ToList();

        var summaries = tagged
            .GroupBy(x => x.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var items = g.Select(x => x.Transaction).ToList();
                var total = items.Sum(t => t.Amount);
                return new TagSummary(
                    g.First().Tag,
                    total,
                    items.Count,
                    yearTotal == 0 ? 0 : total / yearTotal,
                    MonthlySeries(items, year, today));
            })
            .OrderByDescending(s => s.Total)
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

        return new TagReport(
            summaries,
            yearTotal,
            expenses.Count(t => SplitTags(t.Tags).Count > 0),
            expenses.Count);
    }

    private static decimal?[] MonthlySeries(IEnumerable<Transaction> items, int year, DateOnly today)
    {
        var sums = new decimal[12];
        foreach (var t in items)
        {
            sums[t.Date.Month - 1] += t.Amount;
        }
        return Enumerable.Range(1, 12)
            .Select(m => year > today.Year || (year == today.Year && m > today.Month) ? (decimal?)null : sums[m - 1])
            .ToArray();
    }
}
