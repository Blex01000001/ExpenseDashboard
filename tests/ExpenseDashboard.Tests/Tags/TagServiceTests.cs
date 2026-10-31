using ExpenseDashboard.Domain.Transactions;
using ExpenseDashboard.Tests.Data;
using ExpenseDashboard.Web.Tags;

namespace ExpenseDashboard.Tests.Tags;

public class TagServiceTests
{
    [Fact]
    public async Task Report_and_details_are_filtered_by_year_and_currency()
    {
        using var database = await SeedAsync(
            New(new DateOnly(2025, 3, 1), 300m, "旅行, 工作"),
            New(new DateOnly(2025, 4, 1), 100m, "旅行"),
            New(new DateOnly(2025, 4, 2), 50m, "旅行", currency: "JPY"),
            New(new DateOnly(2024, 4, 2), 70m, "旅行"),
            New(new DateOnly(2025, 5, 1), 20m, null));
        var service = new TagService(database, new FixedTimeProvider(new DateOnly(2030, 1, 1)));

        var report = await service.GetReportAsync(2025, "TWD");
        var details = await service.GetExpensesAsync("旅行", 2025, "TWD");

        Assert.Equal(["旅行", "工作"], report.Tags.Select(t => t.Name));
        Assert.Equal(400m, report.Tags[0].Total);
        Assert.Equal([new DateOnly(2025, 4, 1), new DateOnly(2025, 3, 1)], details.Select(d => d.Date));
        Assert.True(await service.AnyTaggedAsync());
    }

    [Fact]
    public async Task AnyTagged_is_false_when_no_expense_has_tags()
    {
        using var database = await SeedAsync(New(new DateOnly(2025, 1, 1), 10m, null), New(new DateOnly(2025, 1, 2), 10m, ""));

        Assert.False(await new TagService(database, TimeProvider.System).AnyTaggedAsync());
    }

    private static async Task<SqliteTestDatabase> SeedAsync(params Transaction[] transactions)
    {
        var database = new SqliteTestDatabase();
        await using var db = database.CreateDbContext();
        db.Transactions.AddRange(transactions);
        await db.SaveChangesAsync();
        return database;
    }

    private static Transaction New(DateOnly date, decimal amount, string? tags, string currency = "TWD") => new()
    {
        ExternalId = Guid.NewGuid(),
        Date = date,
        Category = "午餐",
        Amount = amount,
        Currency = currency,
        Member = "自己",
        Account = "現金",
        Tags = tags,
        Type = TransactionType.Expense,
        UpdatedAt = new DateTime(2025, 1, 4, 12, 0, 0),
    };
}
