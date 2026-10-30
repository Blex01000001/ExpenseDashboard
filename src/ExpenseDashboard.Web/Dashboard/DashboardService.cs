using ExpenseDashboard.Domain.Reports;
using ExpenseDashboard.Domain.Transactions;
using ExpenseDashboard.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseDashboard.Web.Dashboard;

public sealed record DashboardFilter(int Year, string Currency);

public sealed record DashboardOptions(IReadOnlyList<int> Years, IReadOnlyList<string> Currencies);

public sealed record DashboardData(
    YearSummary Year,
    IReadOnlyList<MonthAnalysis> Months,
    IReadOnlyList<CategoryTotal> ByMajorCategory,
    IReadOnlyList<CategoryTotal> TopCategories);

/// <summary>從資料庫取出指定年份與貨幣的支出，交給 <see cref="DashboardCalculator"/> 彙總。</summary>
public class DashboardService(IDbContextFactory<ExpenseDbContext> dbFactory, TimeProvider timeProvider)
{
    public const string DefaultCurrency = "TWD";

    /// <summary>有支出的年份（新到舊）與貨幣（筆數多到少）。</summary>
    public async Task<DashboardOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var expenses = db.Transactions.Where(t => t.Type == TransactionType.Expense);

        var years = await expenses
            .Select(t => t.Date.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(cancellationToken);

        var currencies = await expenses
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

        // 多撈前一年 12 月，讓 1 月也能算「較上月」。
        var from = new DateOnly(filter.Year - 1, 12, 1);
        var to = new DateOnly(filter.Year, 12, 31);
        var transactions = await db.Transactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Expense
                        && t.Currency == filter.Currency
                        && t.Date >= from
                        && t.Date <= to)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var months = DashboardCalculator.AnalyzeMonths(transactions, filter.Year, today);
        var inYear = transactions.Where(t => t.Date.Year == filter.Year).ToList();

        return new DashboardData(
            DashboardCalculator.SummarizeYear(filter.Year, months),
            months,
            DashboardCalculator.ExpenseByMajorCategory(inYear),
            DashboardCalculator.TopExpenseCategories(inYear));
    }
}
