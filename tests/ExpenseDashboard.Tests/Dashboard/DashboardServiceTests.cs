using ExpenseDashboard.Domain.Transactions;
using ExpenseDashboard.Tests.Data;
using ExpenseDashboard.Web.Dashboard;

namespace ExpenseDashboard.Tests.Dashboard;

public class DashboardServiceTests
{
    [Fact]
    public async Task Options_list_expense_years_newest_first_and_currencies_by_frequency()
    {
        using var database = await SeedAsync(
            New(new DateOnly(2024, 6, 1), 10m),
            New(new DateOnly(2025, 1, 1), 10m),
            New(new DateOnly(2025, 2, 1), 10m, currency: "JPY"),
            New(new DateOnly(2023, 3, 1), 10m),
            New(new DateOnly(2022, 3, 1), 10m, type: TransactionType.Income));

        var options = await CreateService(database).GetOptionsAsync();

        Assert.Equal([2025, 2024, 2023], options.Years);
        Assert.Equal(["TWD", "JPY"], options.Currencies);
    }

    [Fact]
    public async Task Options_are_empty_without_data()
    {
        using var database = new SqliteTestDatabase();

        var options = await CreateService(database).GetOptionsAsync();

        Assert.Empty(options.Years);
        Assert.Empty(options.Currencies);
    }

    [Fact]
    public async Task Year_filter_includes_both_ends_of_the_year()
    {
        using var database = await SeedAsync(
            New(new DateOnly(2024, 12, 31), 1m),
            New(new DateOnly(2025, 1, 1), 2m),
            New(new DateOnly(2025, 12, 31), 4m),
            New(new DateOnly(2026, 1, 1), 8m));

        var data = await CreateService(database).LoadAsync(new DashboardFilter(2025, "TWD"));

        Assert.Equal(6m, data.Year.Total);
        Assert.Equal(2, data.Year.Count);
        Assert.Equal(12, data.Months.Count);
        Assert.Equal("2025/01", data.Months[0].Label);
        Assert.Equal("2025/12", data.Months[^1].Label);
    }

    [Fact]
    public async Task January_change_uses_the_previous_december()
    {
        using var database = await SeedAsync(
            New(new DateOnly(2024, 12, 15), 100m),
            New(new DateOnly(2025, 1, 15), 150m));

        var data = await CreateService(database).LoadAsync(new DashboardFilter(2025, "TWD"));

        Assert.Equal(0.5m, Assert.Single(data.Months).ChangeRatio);
        Assert.Equal(150m, data.Year.Total);
    }

    [Fact]
    public async Task Current_month_daily_average_uses_the_clock()
    {
        using var database = await SeedAsync(New(new DateOnly(2025, 10, 1), 500m));

        var data = await CreateService(database, today: new DateOnly(2025, 10, 5))
            .LoadAsync(new DashboardFilter(2025, "TWD"));

        Assert.Equal(100m, Assert.Single(data.Months).DailyAverage);
    }

    [Fact]
    public async Task Currency_filter_never_mixes_currencies()
    {
        using var database = await SeedAsync(
            New(new DateOnly(2025, 1, 1), 100m, currency: "TWD"),
            New(new DateOnly(2025, 1, 2), 5m, currency: "USD"));

        var data = await CreateService(database).LoadAsync(new DashboardFilter(2025, "USD"));

        Assert.Equal(5m, data.Year.Total);
        Assert.Equal(5m, Assert.Single(data.TopCategories).Amount);
    }

    [Fact]
    public async Task Year_without_expenses_is_empty()
    {
        using var database = await SeedAsync(New(new DateOnly(2025, 1, 1), 100m));

        var data = await CreateService(database).LoadAsync(new DashboardFilter(2024, "TWD"));

        Assert.Equal(0, data.Year.Count);
        Assert.Empty(data.Months);
        Assert.Empty(data.ByMajorCategory);
        Assert.Empty(data.TopCategories);
    }

    [Fact]
    public async Task Year_breakdowns_exclude_income_and_the_extra_december()
    {
        using var database = await SeedAsync(
            New(new DateOnly(2025, 1, 1), 300m, category: "房租", majorCategory: "Home"),
            New(new DateOnly(2025, 1, 2), 100m, category: "午餐", majorCategory: "Diet"),
            New(new DateOnly(2025, 1, 3), 1000m, category: "薪水", majorCategory: "Income", type: TransactionType.Income),
            New(new DateOnly(2024, 12, 3), 999m, category: "去年", majorCategory: "Old"));

        var data = await CreateService(database).LoadAsync(new DashboardFilter(2025, "TWD"));

        Assert.Equal(["Home", "Diet"], data.ByMajorCategory.Select(c => c.Name));
        Assert.Equal(["房租", "午餐"], data.TopCategories.Select(c => c.Name));
        Assert.Equal(400m, data.Year.Total);
    }

    private static DashboardService CreateService(SqliteTestDatabase database, DateOnly? today = null) =>
        new(database, new FixedTimeProvider(today ?? new DateOnly(2030, 1, 1)));

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

    private sealed class FixedTimeProvider(DateOnly today) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
    }
}
