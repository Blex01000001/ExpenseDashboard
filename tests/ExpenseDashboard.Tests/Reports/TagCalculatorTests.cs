using ExpenseDashboard.Domain.Reports;
using ExpenseDashboard.Domain.Transactions;

namespace ExpenseDashboard.Tests.Reports;

public class TagCalculatorTests
{
    private static readonly DateOnly LongAfter = new(2030, 1, 1);

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("", new string[0])]
    [InlineData("旅行", new[] { "旅行" })]
    [InlineData("工作, 出差", new[] { "工作", "出差" })]
    [InlineData("工作，出差、日本；東京;  ", new[] { "工作", "出差", "日本", "東京" })]
    [InlineData("日本 旅行", new[] { "日本 旅行" })]
    [InlineData("Work,work,WORK", new[] { "Work" })]
    public void SplitTags_splits_on_commas_and_semicolons_but_not_spaces(string? raw, string[] expected)
    {
        Assert.Equal(expected, TagCalculator.SplitTags(raw));
    }

    [Fact]
    public void Summarize_counts_each_tag_of_a_multi_tag_expense()
    {
        var report = TagCalculator.Summarize(
            [
                Expense(300m, new DateOnly(2025, 1, 5), "旅行,工作"),
                Expense(100m, new DateOnly(2025, 2, 5), "旅行"),
                Expense(600m, new DateOnly(2025, 2, 6), null),
                Expense(999m, new DateOnly(2024, 2, 6), "旅行"),
                Income(5000m, new DateOnly(2025, 2, 6), "旅行"),
            ],
            2025,
            LongAfter);

        Assert.Equal(["旅行", "工作"], report.Tags.Select(t => t.Name));
        var travel = report.Tags[0];
        Assert.Equal(400m, travel.Total);
        Assert.Equal(2, travel.Count);
        Assert.Equal(0.4m, travel.Share);
        Assert.Equal([300m, 100m, 0m], travel.Monthly.Take(3));
        Assert.Equal(1000m, report.YearTotal);
        Assert.Equal(2, report.TaggedCount);
        Assert.Equal(3, report.TotalCount);
    }

    [Fact]
    public void Summarize_treats_tags_case_insensitively_and_leaves_future_months_empty()
    {
        var report = TagCalculator.Summarize(
            [Expense(10m, new DateOnly(2025, 1, 1), "Work"), Expense(20m, new DateOnly(2025, 2, 1), "work")],
            2025,
            today: new DateOnly(2025, 2, 15));

        var work = Assert.Single(report.Tags);
        Assert.Equal("Work", work.Name);
        Assert.Equal(30m, work.Total);
        Assert.Equal([10m, 20m, null], work.Monthly.Take(3));
    }

    [Fact]
    public void Summarize_without_tags_is_empty()
    {
        var report = TagCalculator.Summarize([Expense(10m, new DateOnly(2025, 1, 1), null)], 2025, LongAfter);

        Assert.Empty(report.Tags);
        Assert.Equal(0, report.TaggedCount);
    }

    [Fact]
    public void HasTag_matches_whole_tags_only()
    {
        var t = Expense(10m, new DateOnly(2025, 1, 1), "日本旅行, 工作");

        Assert.True(TagCalculator.HasTag(t, "工作"));
        Assert.False(TagCalculator.HasTag(t, "旅行"));
    }

    private static Transaction Expense(decimal amount, DateOnly date, string? tags) => New(TransactionType.Expense, amount, date, tags);

    private static Transaction Income(decimal amount, DateOnly date, string? tags) => New(TransactionType.Income, amount, date, tags);

    private static Transaction New(TransactionType type, decimal amount, DateOnly date, string? tags) => new()
    {
        ExternalId = Guid.NewGuid(),
        Date = date,
        Category = "午餐",
        Amount = amount,
        Currency = "TWD",
        Member = "自己",
        Account = "現金",
        Tags = tags,
        Type = type,
        UpdatedAt = new DateTime(2025, 1, 4, 12, 0, 0),
    };
}
