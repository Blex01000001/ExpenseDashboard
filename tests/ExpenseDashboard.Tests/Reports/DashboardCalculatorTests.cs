using ExpenseDashboard.Domain.Reports;
using ExpenseDashboard.Domain.Rules;
using ExpenseDashboard.Domain.Transactions;

namespace ExpenseDashboard.Tests.Reports;

public class DashboardCalculatorTests
{
    private static readonly DateOnly LongAfter = new(2030, 1, 1);

    [Fact]
    public void AnalyzeMonths_spans_first_to_last_month_with_expenses_including_gaps()
    {
        var months = DashboardCalculator.AnalyzeMonths(
            [
                Expense(100m, new DateOnly(2025, 3, 3)),
                Expense(50m, new DateOnly(2025, 6, 30)),
            ],
            2025,
            LongAfter);

        Assert.Equal(["2025/03", "2025/04", "2025/05", "2025/06"], months.Select(m => m.Label));
        Assert.Equal([100m, 0m, 0m, 50m], months.Select(m => m.Total));
        Assert.Equal([1, 0, 0, 1], months.Select(m => m.Count));
    }

    [Fact]
    public void AnalyzeMonths_only_covers_the_requested_year()
    {
        var months = DashboardCalculator.AnalyzeMonths(
            [
                Expense(1m, new DateOnly(2024, 12, 31)),
                Expense(2m, new DateOnly(2025, 2, 1)),
                Expense(4m, new DateOnly(2026, 1, 1)),
            ],
            2025,
            LongAfter);

        var month = Assert.Single(months);
        Assert.Equal("2025/02", month.Label);
        Assert.Equal(2m, month.Total);
    }

    [Fact]
    public void AnalyzeMonths_ignores_income()
    {
        var months = DashboardCalculator.AnalyzeMonths(
            [Expense(100m, new DateOnly(2025, 1, 5)), Income(5000m, new DateOnly(2025, 1, 5)), Income(5000m, new DateOnly(2025, 2, 5))],
            2025,
            LongAfter);

        var month = Assert.Single(months);
        Assert.Equal(100m, month.Total);
        Assert.Equal(1, month.Count);
    }

    [Fact]
    public void AnalyzeMonths_is_empty_for_a_year_without_expenses()
    {
        Assert.Empty(DashboardCalculator.AnalyzeMonths([Expense(1m, new DateOnly(2024, 5, 1))], 2025, LongAfter));
    }

    [Fact]
    public void ChangeRatio_compares_with_the_previous_month_including_previous_december()
    {
        var months = DashboardCalculator.AnalyzeMonths(
            [
                Expense(200m, new DateOnly(2024, 12, 10)),
                Expense(300m, new DateOnly(2025, 1, 10)),
                Expense(150m, new DateOnly(2025, 2, 10)),
                Expense(150m, new DateOnly(2025, 3, 10)),
            ],
            2025,
            LongAfter);

        Assert.Equal([0.5m, -0.5m, 0m], months.Select(m => m.ChangeRatio));
    }

    [Fact]
    public void ChangeRatio_is_null_when_previous_month_has_no_expense()
    {
        var months = DashboardCalculator.AnalyzeMonths(
            [Expense(100m, new DateOnly(2025, 1, 10)), Expense(100m, new DateOnly(2025, 3, 10))],
            2025,
            LongAfter);

        Assert.Equal([null, -1m, null], months.Select(m => m.ChangeRatio));
    }

    [Fact]
    public void DailyAverage_divides_by_days_in_month()
    {
        var month = Assert.Single(DashboardCalculator.AnalyzeMonths(
            [Expense(280m, new DateOnly(2025, 2, 3))], 2025, LongAfter));

        Assert.Equal(10m, month.DailyAverage);
    }

    [Fact]
    public void DailyAverage_of_the_current_month_only_counts_days_so_far()
    {
        var month = Assert.Single(DashboardCalculator.AnalyzeMonths(
            [Expense(300m, new DateOnly(2025, 10, 3))], 2025, today: new DateOnly(2025, 10, 10)));

        Assert.Equal(30m, month.DailyAverage);
    }

    [Fact]
    public void SummarizeYear_totals_months_and_finds_the_peak()
    {
        var months = DashboardCalculator.AnalyzeMonths(
            [
                Expense(100m, new DateOnly(2025, 1, 1)),
                Expense(500m, new DateOnly(2025, 2, 1)),
                Expense(50m, new DateOnly(2025, 2, 2)),
                Expense(250m, new DateOnly(2025, 4, 1)),
            ],
            2025,
            LongAfter);

        var summary = DashboardCalculator.SummarizeYear(2025, months);

        Assert.Equal(2025, summary.Year);
        Assert.Equal(900m, summary.Total);
        Assert.Equal(4, summary.Count);
        Assert.Equal(4, summary.MonthCount);
        Assert.Equal(225m, summary.MonthlyAverage);
        Assert.Equal(2, summary.PeakMonth!.Month);
    }

    [Fact]
    public void SummarizeYear_of_nothing_is_zero()
    {
        var summary = DashboardCalculator.SummarizeYear(2025, []);

        Assert.Equal(new YearSummary(2025, 0, 0, 0, 0, null), summary);
    }

    [Fact]
    public void YearOverYearRatio_compares_with_the_same_month_last_year()
    {
        var months = DashboardCalculator.AnalyzeMonths(
            [
                Expense(200m, new DateOnly(2024, 1, 10)),
                Expense(100m, new DateOnly(2024, 2, 10)),
                Expense(300m, new DateOnly(2025, 1, 10)),
                Expense(50m, new DateOnly(2025, 2, 10)),
                Expense(80m, new DateOnly(2025, 3, 10)),
            ],
            2025,
            LongAfter);

        Assert.Equal([0.5m, -0.5m, null], months.Select(m => m.YearOverYearRatio));
    }

    [Fact]
    public void Month_breakdown_lists_featured_major_categories_in_year_order_then_other()
    {
        // 年度排名：Home 600 > Diet 400 > 交通 100；本月沒有交通。
        var months = DashboardCalculator.AnalyzeMonths(
            [
                Expense(600m, new DateOnly(2025, 1, 5), majorCategory: "Home"),
                Expense(100m, new DateOnly(2025, 1, 6), majorCategory: "交通"),
                Expense(300m, new DateOnly(2025, 2, 2), majorCategory: "Diet"),
                Expense(100m, new DateOnly(2025, 2, 3), majorCategory: "Diet"),
            ],
            2025,
            LongAfter);

        Assert.Equal(["Home", "交通"], months[0].MajorCategories.Select(c => c.Name));
        Assert.Equal([new CategoryTotal("Diet", 400m, 1m)], months[1].MajorCategories);
    }

    [Fact]
    public void Month_breakdown_folds_non_featured_major_categories_into_other()
    {
        var transactions = Enumerable.Range(1, 7)
            .Select(i => Expense(i * 10m, new DateOnly(2025, 1, i), majorCategory: $"C{i}"))
            .Append(Expense(5m, new DateOnly(2025, 1, 9), majorCategory: null))
            .ToList();

        var month = Assert.Single(DashboardCalculator.AnalyzeMonths(transactions, 2025, LongAfter));

        Assert.Equal(["C7", "C6", "C5", "C4", "C3", DashboardCalculator.OtherName], month.MajorCategories.Select(c => c.Name));
        Assert.Equal(35m, month.MajorCategories[^1].Amount);
        Assert.Equal(1m, month.MajorCategories.Sum(c => c.Share));
    }

    [Fact]
    public void Category_without_major_category_is_its_own_slice()
    {
        var month = Assert.Single(DashboardCalculator.AnalyzeMonths(
            [
                Expense(300m, new DateOnly(2025, 1, 1), category: "運動", majorCategory: null),
                Expense(100m, new DateOnly(2025, 1, 2), category: "ゲーム", majorCategory: null),
                Expense(200m, new DateOnly(2025, 1, 3), category: "晚餐", majorCategory: "Diet"),
            ],
            2025,
            LongAfter));

        Assert.Equal(["運動", "Diet", "ゲーム"], month.MajorCategories.Select(c => c.Name));
        Assert.DoesNotContain(month.MajorCategories, c => c.Name == DashboardCalculator.UncategorizedName);
    }

    [Fact]
    public void FeaturedMajorCategories_uses_category_name_when_major_category_is_missing()
    {
        var featured = DashboardCalculator.FeaturedMajorCategories(
            [
                Expense(300m, new DateOnly(2025, 1, 1), category: "運動", majorCategory: null),
                Expense(200m, new DateOnly(2025, 1, 2), majorCategory: "Diet"),
                Expense(999m, new DateOnly(2024, 1, 2), majorCategory: "去年"),
            ],
            2025);

        Assert.Equal(["運動", "Diet"], featured);
    }

    [Fact]
    public void Keyword_trend_items_match_notes_case_insensitively_and_overlap_categories()
    {
        var rule = new KeywordRule { Id = 7, Name = "Steam 遊戲", Keywords = ["steam", "BA"] };

        var items = DashboardCalculator.TrendItems(
            [
                Expense(300m, new DateOnly(2025, 1, 5), category: "ゲーム", majorCategory: null, note: "Steam 特價"),
                Expense(100m, new DateOnly(2025, 2, 5), category: "ゲーム", majorCategory: null, note: "BA 課金"),
                Expense(50m, new DateOnly(2025, 2, 6), category: "晚餐", note: "跟朋友"),
                Expense(999m, new DateOnly(2024, 2, 6), category: "ゲーム", note: "steam 去年"),
            ],
            2025,
            new DateOnly(2025, 3, 31),
            [rule]);

        var keyword = items.Single(i => i.Kind == TrendItemKind.Keyword);
        Assert.Equal("k:7", keyword.Id);
        Assert.Equal("Steam 遊戲（備註）", keyword.DisplayName);
        Assert.Equal(400m, keyword.YearTotal);
        Assert.Equal([300m, 100m, 0m, null], keyword.Monthly.Take(4));
        // 同一筆支出仍照常算在原本的類別。
        Assert.Equal(400m, items.Single(i => i.Id == "c:ゲーム").YearTotal);
    }

    [Fact]
    public void Keyword_rules_without_matches_are_still_listed_last()
    {
        var items = DashboardCalculator.TrendItems(
            [Expense(10m, new DateOnly(2025, 1, 1), note: "abc")],
            2025,
            LongAfter,
            [new KeywordRule { Id = 1, Name = "B", Keywords = ["zzz"] }, new KeywordRule { Id = 2, Name = "A", Keywords = ["abc"] }]);

        Assert.Equal(["k:2", "k:1"], items.Where(i => i.Kind == TrendItemKind.Keyword).Select(i => i.Id));
        Assert.Equal(TrendItemKind.Keyword, items[^1].Kind);
        Assert.All(items.Single(i => i.Id == "k:1").Monthly, v => Assert.Equal(0m, v));
    }

    [Fact]
    public void CompareWithPreviousYear_of_the_current_year_stops_at_today()
    {
        var comparison = DashboardCalculator.CompareWithPreviousYear(
            [
                Expense(100m, new DateOnly(2024, 1, 1)),
                Expense(50m, new DateOnly(2024, 10, 15)),
                Expense(999m, new DateOnly(2024, 10, 16)),   // 去年同期之後，不算
                Expense(300m, new DateOnly(2025, 3, 1)),
                Income(5000m, new DateOnly(2025, 3, 1)),
                Expense(999m, new DateOnly(2023, 12, 31)),
            ],
            2025,
            today: new DateOnly(2025, 10, 15));

        Assert.Equal(new DateOnly(2025, 10, 15), comparison.PeriodEnd);
        Assert.Equal(300m, comparison.CurrentTotal);
        Assert.Equal(150m, comparison.PreviousTotal);
        Assert.Equal(1m, comparison.Ratio);
    }

    [Fact]
    public void CompareWithPreviousYear_of_a_past_year_uses_the_whole_year()
    {
        var comparison = DashboardCalculator.CompareWithPreviousYear(
            [Expense(200m, new DateOnly(2023, 12, 31)), Expense(150m, new DateOnly(2024, 12, 31))],
            2024,
            today: new DateOnly(2025, 6, 1));

        Assert.Equal(new DateOnly(2024, 12, 31), comparison.PeriodEnd);
        Assert.Equal(-0.25m, comparison.Ratio);
    }

    [Fact]
    public void CompareWithPreviousYear_is_null_without_previous_spending_and_handles_leap_day()
    {
        var comparison = DashboardCalculator.CompareWithPreviousYear(
            [Expense(10m, new DateOnly(2024, 2, 29))],
            2024,
            today: new DateOnly(2024, 2, 29));

        Assert.Null(comparison.Ratio);
        Assert.Equal(0m, comparison.PreviousTotal);
    }

    [Fact]
    public void Empty_month_has_no_breakdown()
    {
        var months = DashboardCalculator.AnalyzeMonths(
            [Expense(1m, new DateOnly(2025, 1, 1)), Expense(1m, new DateOnly(2025, 3, 1))],
            2025,
            LongAfter);

        Assert.Empty(months[1].MajorCategories);
        Assert.Equal(0m, months[1].DailyAverage);
    }

    [Fact]
    public void MonthlyTotals_covers_twelve_months_with_zero_for_past_and_null_for_future()
    {
        var totals = DashboardCalculator.MonthlyTotals(
            [
                Expense(100m, new DateOnly(2025, 1, 5)),
                Expense(50m, new DateOnly(2025, 3, 5)),
                Expense(999m, new DateOnly(2024, 3, 5)),
                Income(5000m, new DateOnly(2025, 1, 5)),
            ],
            2025,
            today: new DateOnly(2025, 4, 15));

        Assert.Equal(12, totals.Count);
        Assert.Equal("1 月", totals[0].Label);
        Assert.Equal([100m, 0m, 50m, 0m, null, null, null, null, null, null, null, null], totals.Select(t => t.Total));
    }

    [Fact]
    public void MonthlyTotals_of_a_past_year_has_no_future_months()
    {
        var totals = DashboardCalculator.MonthlyTotals([Expense(1m, new DateOnly(2024, 6, 1))], 2024, new DateOnly(2025, 1, 1));

        Assert.All(totals, t => Assert.NotNull(t.Total));
    }

    [Fact]
    public void TrendItems_lists_explicit_major_categories_then_categories_by_year_total()
    {
        var items = DashboardCalculator.TrendItems(
            [
                Expense(100m, new DateOnly(2025, 1, 5), category: "晚餐", majorCategory: "Diet"),
                Expense(30m, new DateOnly(2025, 2, 5), category: "早餐", majorCategory: "Diet"),
                Expense(300m, new DateOnly(2025, 2, 6), category: "運動", majorCategory: null),
                Expense(999m, new DateOnly(2024, 2, 6), category: "去年", majorCategory: "Old"),
            ],
            2025,
            new DateOnly(2025, 3, 31));

        Assert.Equal(
            // 「運動」沒有大類別：只出現在類別組，不再另外多一條一模一樣的大類別線。
            ["m:Diet", "c:運動", "c:晚餐", "c:早餐"],
            items.Select(i => i.Id));

        var diet = items.Single(i => i.Id == "m:Diet");
        Assert.Equal(130m, diet.YearTotal);
        Assert.Equal([100m, 30m, 0m, null], diet.Monthly.Take(4));
        Assert.Equal(12, diet.Monthly.Count);
    }

    [Fact]
    public void DefaultTrendItems_are_the_top_categories_not_major_categories()
    {
        var items = DashboardCalculator.TrendItems(
            [
                Expense(500m, category: "房租", majorCategory: "Home"),
                Expense(300m, category: "晚餐", majorCategory: "Diet"),
                Expense(200m, category: "午餐", majorCategory: "Diet"),
                Expense(100m, category: "早餐", majorCategory: "Diet"),
            ],
            2025,
            LongAfter);

        var defaults = DashboardCalculator.DefaultTrendItems(items);

        Assert.Equal(["c:房租", "c:晚餐", "c:午餐"], defaults.Select(i => i.Id));
    }

    private static Transaction Expense(
        decimal amount, DateOnly? date = null, string category = "午餐", string? majorCategory = "Diet", string? note = null)
    {
        var transaction = New(TransactionType.Expense, amount, date, category, majorCategory);
        transaction.Note = note;
        return transaction;
    }

    private static Transaction Income(decimal amount, DateOnly? date = null) =>
        New(TransactionType.Income, amount, date, "薪水", "Income");

    private static Transaction New(TransactionType type, decimal amount, DateOnly? date, string category, string? majorCategory) => new()
    {
        ExternalId = Guid.NewGuid(),
        Date = date ?? new DateOnly(2025, 1, 4),
        Category = category,
        MajorCategory = majorCategory,
        Amount = amount,
        Currency = "TWD",
        Member = "自己",
        Account = "現金",
        Type = type,
        UpdatedAt = new DateTime(2025, 1, 4, 12, 0, 0),
    };
}
