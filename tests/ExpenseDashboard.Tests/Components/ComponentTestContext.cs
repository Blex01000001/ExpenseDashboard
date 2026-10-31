using ApexCharts;
using Bunit;
using ExpenseDashboard.Domain.Transactions;
using ExpenseDashboard.Tests.Data;
using ExpenseDashboard.Web.Dashboard;
using ExpenseDashboard.Web.Data;
using ExpenseDashboard.Web.Import;
using ExpenseDashboard.Web.Rules;
using ExpenseDashboard.Web.Tags;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseDashboard.Tests.Components;

/// <summary>
/// bUnit 測試共用環境：真的 in-memory SQLite 與所有服務、固定的「今天」、
/// JS interop 寬鬆模式（localStorage 讀到 null），ApexCharts 圖表換成空的 stub（圖表本身靠瀏覽器 JS 繪製）。
/// </summary>
public abstract class ComponentTestContext : BunitContext
{
    protected static readonly DateOnly Today = new(2026, 10, 15);

    protected ComponentTestContext()
    {
        Database = new SqliteTestDatabase();
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.AddStub(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApexChart<>));

        Services.AddSingleton<IDbContextFactory<ExpenseDbContext>>(Database);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Today));
        Services.AddScoped<DashboardService>();
        Services.AddScoped<TransactionImporter>();
        Services.AddScoped<KeywordRuleService>();
        Services.AddScoped<TagService>();
    }

    protected SqliteTestDatabase Database { get; }

    protected async Task SeedAsync(params Transaction[] transactions)
    {
        await using var db = Database.CreateDbContext();
        db.Transactions.AddRange(transactions);
        await db.SaveChangesAsync();
    }

    protected static Transaction Expense(
        DateOnly date, decimal amount, string category = "午餐", string? majorCategory = "Diet",
        string? note = null, string? tags = null) => new()
    {
        ExternalId = Guid.NewGuid(),
        Date = date,
        Category = category,
        MajorCategory = majorCategory,
        Amount = amount,
        Currency = "TWD",
        Member = "自己",
        Account = "現金",
        Note = note,
        Tags = tags,
        Type = TransactionType.Expense,
        UpdatedAt = date.ToDateTime(new TimeOnly(12, 0)),
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Database.Dispose();
        }
        base.Dispose(disposing);
    }
}
