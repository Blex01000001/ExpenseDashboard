using ExpenseDashboard.Domain.Reports;
using ExpenseDashboard.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseDashboard.Web.Dashboard;

/// <summary>儀表板篩選條件；<see cref="Year"/> 為 null 表示全部期間。</summary>
public sealed record DashboardFilter(int? Year, string Currency);

public sealed record DashboardOptions(IReadOnlyList<int> Years, IReadOnlyList<string> Currencies);

public sealed record DashboardData(
    PeriodSummary Summary,
    IReadOnlyList<MonthlyTotal> Monthly,
    IReadOnlyList<CategoryTotal> ByMajorCategory,
    IReadOnlyList<CategoryTotal> TopCategories);

/// <summary>從資料庫取出符合篩選條件的交易，交給 <see cref="DashboardCalculator"/> 彙總。</summary>
public class DashboardService(IDbContextFactory<ExpenseDbContext> dbFactory)
{
    public const string DefaultCurrency = "TWD";

    /// <summary>資料中出現過的年份（新到舊）與貨幣（筆數多到少）。</summary>
    public async Task<DashboardOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var years = await db.Transactions
            .Select(t => t.Date.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(cancellationToken);

        var currencies = await db.Transactions
            .GroupBy(t => t.Currency)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);

        return new DashboardOptions(years, currencies);
    }

    public async Task<DashboardData> LoadAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Transactions.AsNoTracking().Where(t => t.Currency == filter.Currency);
        if (filter.Year is int year)
        {
            var start = new DateOnly(year, 1, 1);
            var end = new DateOnly(year, 12, 31);
            query = query.Where(t => t.Date >= start && t.Date <= end);
        }
        var transactions = await query.ToListAsync(cancellationToken);

        // 指定年份時固定顯示 1–12 月；全部期間則從最早到最晚有資料的月份。
        var (from, to) = filter.Year is int y
            ? (new DateOnly(y, 1, 1), new DateOnly(y, 12, 31))
            : transactions.Count > 0
                ? (transactions.Min(t => t.Date), transactions.Max(t => t.Date))
                : (DateOnly.MaxValue, DateOnly.MinValue);

        return new DashboardData(
            DashboardCalculator.Summarize(transactions),
            DashboardCalculator.MonthlyTotals(transactions, from, to),
            DashboardCalculator.ExpenseByMajorCategory(transactions),
            DashboardCalculator.TopExpenseCategories(transactions));
    }
}
