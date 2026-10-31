using ExpenseDashboard.Domain.Reports;
using ExpenseDashboard.Domain.Transactions;
using ExpenseDashboard.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseDashboard.Web.Tags;

/// <summary>標籤明細的一筆支出。</summary>
public sealed record TaggedExpense(DateOnly Date, string Category, decimal Amount, string Currency, string? Note, string Tags);

/// <summary>標籤彙總頁的資料：依年度與貨幣篩選支出，交給 <see cref="TagCalculator"/>。</summary>
public class TagService(IDbContextFactory<ExpenseDbContext> dbFactory, TimeProvider timeProvider)
{
    public async Task<TagReport> GetReportAsync(int year, string currency, CancellationToken cancellationToken = default)
    {
        var expenses = await LoadYearAsync(year, currency, cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return TagCalculator.Summarize(expenses, year, today);
    }

    /// <summary>該年度帶有 <paramref name="tag"/>（不分大小寫）的支出，依日期新到舊。</summary>
    public async Task<IReadOnlyList<TaggedExpense>> GetExpensesAsync(
        string tag, int year, string currency, CancellationToken cancellationToken = default)
    {
        var expenses = await LoadYearAsync(year, currency, cancellationToken);
        return expenses
            .Where(t => TagCalculator.HasTag(t, tag))
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.UpdatedAt)
            .Select(t => new TaggedExpense(t.Date, t.Category, t.Amount, t.Currency, t.Note, t.Tags!))
            .ToList();
    }

    /// <summary>資料庫裡是否有任何帶標籤的支出（決定要顯示說明還是彙總）。</summary>
    public async Task<bool> AnyTaggedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Transactions.AnyAsync(
            t => t.Type == TransactionType.Expense && t.Tags != null && t.Tags != "", cancellationToken);
    }

    private async Task<List<Transaction>> LoadYearAsync(int year, string currency, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        return await db.Transactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Expense && t.Currency == currency && t.Date >= from && t.Date <= to)
            .ToListAsync(cancellationToken);
    }
}
