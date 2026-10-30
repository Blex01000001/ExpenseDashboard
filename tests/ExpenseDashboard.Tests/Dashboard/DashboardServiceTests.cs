using ExpenseDashboard.Domain.Transactions;
using ExpenseDashboard.Tests.Data;
using ExpenseDashboard.Web.Dashboard;

namespace ExpenseDashboard.Tests.Dashboard;

public class DashboardServiceTests
{
    [Fact]
    public async Task Options_list_years_newest_first_and_currencies_by_frequency()
    {
        using var database = await SeedAsync(
            New(new DateOnly(2024, 6, 1), 10m),
            New(new DateOnly(2025, 1, 1), 10m),
            New(new DateOnly(2025, 2, 1), 10m, currency: "JPY"),
            New(new DateOnly(2023, 3, 1), 10m));

        var options = await new DashboardService(database).GetOptionsAsync();

        Assert.Equal([2025, 2024, 2023], options.Years);
        Assert.Equal(["TWD", "JPY"], options.Currencies);
    }

    [Fact]
    public async Task Options_are_empty_without_data()
    {
        using var database = new SqliteTestDatabase();

        var options = await new DashboardService(database).GetOptionsAsync();

        Assert.Empty(options.Years);
        Assert.Empty(options.Currencies);
    }

    [Fact]
    public async Task Year_filter_includes_both_ends_and_always_shows_twelve_months()
    {
        using var database = await SeedAsync(
            New(new DateOnly(2024, 12, 31), 1m),
            New(new DateOnly(2025, 1, 1), 2m),
            New(new DateOnly(2025, 12, 31), 4m),
            New(new DateOnly(2026, 1, 1), 8m));

        var data = await new DashboardService(database).LoadAsync(new DashboardFilter(2025, "TWD"));

        Assert.Equal(6m, data.Summary.Expense);
        Assert.Equal(2, data.Summary.Count);
        Assert.Equal(12, data.Monthly.Count);
        Assert.Equal("2025/01", data.Monthly[0].Label);
        Assert.Equal("2025/12", data.Monthly[^1].Label);
    }

    [Fact]
    public async Task All_periods_span_from_first_to_last_month_with_data()
    {
        using var database = await SeedAsync(
            New(new DateOnly(2024, 11, 20), 1m),
            New(new DateOnly(2025, 2, 3), 2m));

        var data = await new DashboardService(database).LoadAsync(new DashboardFilter(null, "TWD"));

        Assert.Equal(["2024/11", "2024/12", "2025/01", "2025/02"], data.Monthly.Select(m => m.Label));
        Assert.Equal(3m, data.Summary.Expense);
    }

    [Fact]
    public async Task Currency_filter_never_mixes_currencies()
    {
        using var database = await SeedAsync(
            New(new DateOnly(2025, 1, 1), 100m, currency: "TWD"),
            New(new DateOnly(2025, 1, 2), 5m, currency: "USD"));

        var data = await new DashboardService(database).LoadAsync(new DashboardFilter(2025, "USD"));

        Assert.Equal(5m, data.Summary.Expense);
        Assert.Equal(5m, Assert.Single(data.TopCategories).Amount);
    }

    [Fact]
    public async Task Empty_result_has_zero_summary_and_no_breakdowns()
    {
        using var database = await SeedAsync(New(new DateOnly(2025, 1, 1), 100m));

        var data = await new DashboardService(database).LoadAsync(new DashboardFilter(null, "JPY"));

        Assert.Equal(0, data.Summary.Count);
        Assert.Empty(data.Monthly);
        Assert.Empty(data.ByMajorCategory);
        Assert.Empty(data.TopCategories);
    }

    [Fact]
    public async Task Breakdowns_come_from_the_filtered_transactions()
    {
        using var database = await SeedAsync(
            New(new DateOnly(2025, 1, 1), 300m, category: "房租", majorCategory: "Home"),
            New(new DateOnly(2025, 1, 2), 100m, category: "午餐", majorCategory: "Diet"),
            New(new DateOnly(2025, 1, 3), 1000m, category: "薪水", majorCategory: "Income", type: TransactionType.Income),
            New(new DateOnly(2024, 1, 3), 999m, category: "舊資料", majorCategory: "Old"));

        var data = await new DashboardService(database).LoadAsync(new DashboardFilter(2025, "TWD"));

        Assert.Equal(["Home", "Diet"], data.ByMajorCategory.Select(c => c.Name));
        Assert.Equal(["房租", "午餐"], data.TopCategories.Select(c => c.Name));
        Assert.Equal(1000m, data.Summary.Income);
        Assert.Equal(600m, data.Summary.Net);
    }

    private static async Task<SqliteTestDatabase> SeedAsync(params Transaction[] transactions)
    {
        var database = new SqliteTestDatabase();
        await using var db = database.CreateDbContext();
        db.Transactions.AddRange(transactions);
        await db.SaveChangesAsync();
        return database;
    }

    private static Transaction New(
        DateOnly date,
        decimal amount,
        string currency = "TWD",
        string category = "午餐",
        string? majorCategory = "Diet",
        TransactionType type = TransactionType.Expense) => new()
    {
        ExternalId = Guid.NewGuid(),
        Date = date,
        Category = category,
        MajorCategory = majorCategory,
        Amount = amount,
        Currency = currency,
        Member = "自己",
        Account = "現金",
        Type = type,
        UpdatedAt = new DateTime(2025, 1, 4, 12, 0, 0),
    };
}
