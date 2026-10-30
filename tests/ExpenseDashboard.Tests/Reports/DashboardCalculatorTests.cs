using ExpenseDashboard.Domain.Reports;
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
    public void Month_has_major_category_shares_and_largest_expense()
    {
        var month = Assert.Single(DashboardCalculator.AnalyzeMonths(
            [
                Expense(600m, new DateOnly(2025, 1, 20), category: "房租", majorCategory: "Home"),
                Expense(300m, new DateOnly(2025, 1, 2), category: "晚餐", majorCategory: "Diet"),
                Expense(100m, new DateOnly(2025, 1, 3), category: "早餐", majorCategory: "Diet"),
            ],
            2025,
            LongAfter));

        Assert.Equal(
            [new CategoryTotal("Home", 600m, 0.6m), new CategoryTotal("Diet", 400m, 0.4m)],
            month.MajorCategories);
        Assert.Equal(new LargestExpense(new DateOnly(2025, 1, 20), "房租", 600m), month.Largest);
    }

    [Fact]
    public void Largest_expense_tie_picks_the_earliest_date()
    {
        var month = Assert.Single(DashboardCalculator.AnalyzeMonths(
            [
                Expense(100m, new DateOnly(2025, 1, 20), category: "後"),
                Expense(100m, new DateOnly(2025, 1, 5), category: "先"),
            ],
            2025,
            LongAfter));

        Assert.Equal("先", month.Largest!.Category);
    }

    [Fact]
    public void Month_categories_fold_into_other_beyond_the_card_limit()
    {
        var transactions = Enumerable.Range(1, 6)
            .Select(i => Expense(i * 10m, new DateOnly(2025, 1, i), majorCategory: $"C{i}"))
            .ToList();

        var month = Assert.Single(DashboardCalculator.AnalyzeMonths(transactions, 2025, LongAfter));

        Assert.Equal(DashboardCalculator.MonthCategoryCount, month.MajorCategories.Count);
        Assert.Equal(DashboardCalculator.OtherName, month.MajorCategories[^1].Name);
        Assert.Equal(60m, month.MajorCategories[^1].Amount);
    }

    [Fact]
    public void Empty_month_has_no_categories_or_largest_expense()
    {
        var months = DashboardCalculator.AnalyzeMonths(
            [Expense(1m, new DateOnly(2025, 1, 1)), Expense(1m, new DateOnly(2025, 3, 1))],
            2025,
            LongAfter);

        Assert.Empty(months[1].MajorCategories);
        Assert.Null(months[1].Largest);
        Assert.Equal(0m, months[1].DailyAverage);
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
    public void ExpenseByMajorCategory_sorts_descending_and_computes_share()
    {
        var totals = DashboardCalculator.ExpenseByMajorCategory(
        [
            Expense(100m, majorCategory: "Diet"),
            Expense(50m, majorCategory: "Diet"),
            Expense(250m, majorCategory: "Home"),
            Expense(100m, majorCategory: null),
            Income(9999m),
        ]);

        Assert.Equal(
            [
                new CategoryTotal("Home", 250m, 0.5m),
                new CategoryTotal("Diet", 150m, 0.3m),
                new CategoryTotal(DashboardCalculator.UncategorizedName, 100m, 0.2m),
            ],
            totals);
    }

    [Fact]
    public void ExpenseByMajorCategory_folds_the_tail_into_other()
    {
        var transactions = Enumerable.Range(1, 5)
            .Select(i => Expense(i * 10m, majorCategory: $"C{i}"))
            .ToList();

        var totals = DashboardCalculator.ExpenseByMajorCategory(transactions, maxItems: 3);

        Assert.Equal(["C5", "C4", DashboardCalculator.OtherName], totals.Select(t => t.Name));
        Assert.Equal(60m, totals[^1].Amount);
        Assert.Equal(1m, totals.Sum(t => t.Share));
    }

    [Fact]
    public void ExpenseByMajorCategory_does_not_fold_when_within_limit()
    {
        var totals = DashboardCalculator.ExpenseByMajorCategory(
            [Expense(10m, majorCategory: "A"), Expense(20m, majorCategory: "B")],
            maxItems: 2);

        Assert.Equal(["B", "A"], totals.Select(t => t.Name));
    }

    [Fact]
    public void Ties_are_ordered_by_name_for_stable_output()
    {
        var totals = DashboardCalculator.ExpenseByMajorCategory(
            [Expense(10m, majorCategory: "B"), Expense(10m, majorCategory: "A")]);

        Assert.Equal(["A", "B"], totals.Select(t => t.Name));
    }

    [Fact]
    public void TopExpenseCategories_keeps_only_the_top_n_without_other()
    {
        var transactions = Enumerable.Range(1, 12)
            .Select(i => Expense(i, category: $"C{i:00}"))
            .ToList();

        var top = DashboardCalculator.TopExpenseCategories(transactions, count: 10);

        Assert.Equal(10, top.Count);
        Assert.Equal("C12", top[0].Name);
        Assert.Equal("C03", top[^1].Name);
        Assert.DoesNotContain(top, t => t.Name == DashboardCalculator.OtherName);
        // 占比以全部支出為分母，不是只算前 N 名。
        Assert.Equal(12m / 78m, top[0].Share);
    }

    [Fact]
    public void Category_breakdowns_are_empty_without_expenses()
    {
        Transaction[] onlyIncome = [Income(100m)];

        Assert.Empty(DashboardCalculator.ExpenseByMajorCategory(onlyIncome));
        Assert.Empty(DashboardCalculator.TopExpenseCategories(onlyIncome));
    }

    [Fact]
    public void Category_breakdowns_reject_non_positive_limits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DashboardCalculator.ExpenseByMajorCategory([], maxItems: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DashboardCalculator.TopExpenseCategories([], count: 0));
    }

    private static Transaction Expense(decimal amount, DateOnly? date = null, string category = "午餐", string? majorCategory = "Diet") =>
        New(TransactionType.Expense, amount, date, category, majorCategory);

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
