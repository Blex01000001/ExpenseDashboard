using ExpenseDashboard.Domain.Transactions;
using ExpenseDashboard.Tests.Data;
using ExpenseDashboard.Web.Rules;

namespace ExpenseDashboard.Tests.Rules;

public class KeywordRuleServiceTests
{
    [Fact]
    public async Task Added_rule_is_listed_with_its_matches()
    {
        using var database = new SqliteTestDatabase();
        await SeedAsync(database,
            Expense(300m, "Steam 特價"),
            Expense(100m, "ba 課金"),
            Expense(50m, "晚餐"),
            Expense(70m, null),
            Expense(999m, "Steam 退款", TransactionType.Income));
        var service = new KeywordRuleService(database);

        var result = await service.SaveAsync(null, "  遊戲  ", "Steam、BA");
        var summary = Assert.Single(await service.ListAsync());

        Assert.True(result.Succeeded);
        Assert.Equal("遊戲", summary.Rule.Name);
        Assert.Equal(["Steam", "BA"], summary.Rule.Keywords);
        Assert.Equal(2, summary.MatchCount);
        Assert.Equal(400m, summary.MatchTotal);
    }

    [Fact]
    public async Task GetMatches_lists_matching_expenses_newest_first_with_match_position()
    {
        using var database = new SqliteTestDatabase();
        await SeedAsync(database,
            Expense(100m, "BA 課金", date: new DateOnly(2025, 1, 2)),
            Expense(300m, "linebank 轉帳", date: new DateOnly(2025, 1, 3)),
            Expense(200m, "買BA石頭", date: new DateOnly(2025, 2, 1)),
            Expense(999m, "BA 退款", TransactionType.Income));
        var service = new KeywordRuleService(database);
        await service.SaveAsync(null, "遊戲", "BA");
        var id = (await service.ListAsync())[0].Rule.Id;

        var matches = await service.GetMatchesAsync(id);

        Assert.Equal([new DateOnly(2025, 2, 1), new DateOnly(2025, 1, 2)], matches.Select(m => m.Date));
        Assert.Equal(200m, matches[0].Amount);
        Assert.Equal(("買BA石頭", 1, 2), (matches[0].Note, matches[0].MatchIndex, matches[0].MatchLength));
        Assert.Equal(300m, (await service.ListAsync())[0].MatchTotal);
    }

    [Fact]
    public async Task GetMatches_of_unknown_rule_is_empty()
    {
        using var database = new SqliteTestDatabase();

        Assert.Empty(await new KeywordRuleService(database).GetMatchesAsync(42));
    }

    [Fact]
    public async Task Invalid_input_is_rejected_without_saving()
    {
        using var database = new SqliteTestDatabase();
        var service = new KeywordRuleService(database);

        var result = await service.SaveAsync(null, "", " , ");

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Errors.Count);
        Assert.Empty(await service.ListAsync());
    }

    [Fact]
    public async Task Duplicate_name_is_rejected()
    {
        using var database = new SqliteTestDatabase();
        var service = new KeywordRuleService(database);
        await service.SaveAsync(null, "遊戲", "Steam");

        var result = await service.SaveAsync(null, "遊戲", "BA");

        Assert.Contains("遊戲", Assert.Single(result.Errors));
        Assert.Single(await service.ListAsync());
    }

    [Fact]
    public async Task Editing_keeps_the_id_and_may_keep_its_own_name()
    {
        using var database = new SqliteTestDatabase();
        var service = new KeywordRuleService(database);
        await service.SaveAsync(null, "遊戲", "Steam");
        var id = (await service.ListAsync())[0].Rule.Id;

        var result = await service.SaveAsync(id, "遊戲", "Steam\nSwitch");
        var rule = Assert.Single(await service.ListAsync()).Rule;

        Assert.True(result.Succeeded);
        Assert.Equal(id, rule.Id);
        Assert.Equal(["Steam", "Switch"], rule.Keywords);
    }

    [Fact]
    public async Task Editing_a_deleted_rule_reports_an_error()
    {
        using var database = new SqliteTestDatabase();
        var service = new KeywordRuleService(database);

        var result = await service.SaveAsync(42, "遊戲", "Steam");

        Assert.False(result.Succeeded);
        Assert.Empty(await service.ListAsync());
    }

    [Fact]
    public async Task Delete_removes_the_rule()
    {
        using var database = new SqliteTestDatabase();
        var service = new KeywordRuleService(database);
        await service.SaveAsync(null, "遊戲", "Steam");
        var id = (await service.ListAsync())[0].Rule.Id;

        await service.DeleteAsync(id);

        Assert.Empty(await service.ListAsync());
    }

    private static async Task SeedAsync(SqliteTestDatabase database, params Transaction[] transactions)
    {
        await using var db = database.CreateDbContext();
        db.Transactions.AddRange(transactions);
        await db.SaveChangesAsync();
    }

    private static Transaction Expense(
        decimal amount, string? note, TransactionType type = TransactionType.Expense, DateOnly? date = null) => new()
    {
        ExternalId = Guid.NewGuid(),
        Date = date ?? new DateOnly(2025, 1, 4),
        Category = "ゲーム",
        Amount = amount,
        Currency = "TWD",
        Member = "自己",
        Account = "現金",
        Note = note,
        Type = type,
        UpdatedAt = new DateTime(2025, 1, 4, 12, 0, 0),
    };
}
