using ExpenseDashboard.Domain.Reports;
using ExpenseDashboard.Domain.Transactions;

namespace ExpenseDashboard.Tests.Reports;

public class DashboardCalculatorTests
{
    [Fact]
    public void Summarize_totals_income_and_expense_separately()
    {
        var summary = DashboardCalculator.Summarize(
        [
            Expense(100m),
            Expense(50.5m),
            Income(1000m),
        ]);

        Assert.Equal(1000m, summary.Income);
        Assert.Equal(150.5m, summary.Expense);
        Assert.Equal(849.5m, summary.Net);
        Assert.Equal(3, summary.Count);
    }

    [Fact]
    public void Summarize_of_nothing_is_zero()
    {
        Assert.Equal(new PeriodSummary(0, 0, 0), DashboardCalculator.Summarize([]));
    }

    [Fact]
    public void MonthlyTotals_returns_every_month_in_range_including_empty_ones()
    {
        var monthly = DashboardCalculator.MonthlyTotals(
            [
                Expense(100m, new DateOnly(2025, 1, 3)),
                Expense(20m, new DateOnly(2025, 1, 31)),
                Income(500m, new DateOnly(2025, 3, 5)),
            ],
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 4, 30));

        Assert.Equal(["2025/01", "2025/02", "2025/03", "2025/04"], monthly.Select(m => m.Label));
        Assert.Equal([120m, 0m, 0m, 0m], monthly.Select(m => m.Expense));
        Assert.Equal([0m, 0m, 500m, 0m], monthly.Select(m => m.Income));
        Assert.Equal(-120m, monthly[0].Net);
    }

    [Fact]
    public void MonthlyTotals_spans_year_boundaries()
    {
        var monthly = DashboardCalculator.MonthlyTotals(
            [Expense(10m, new DateOnly(2024, 12, 15)), Expense(20m, new DateOnly(2025, 1, 15))],
            new DateOnly(2024, 11, 20),
            new DateOnly(2025, 1, 2));

        Assert.Equal(["2024/11", "2024/12", "2025/01"], monthly.Select(m => m.Label));
    }

    [Fact]
    public void MonthlyTotals_ignores_transactions_outside_the_range()
    {
        var monthly = DashboardCalculator.MonthlyTotals(
            [
                Expense(1m, new DateOnly(2024, 12, 31)),
                Expense(2m, new DateOnly(2025, 1, 1)),
                Expense(4m, new DateOnly(2025, 1, 31)),
                Expense(8m, new DateOnly(2025, 2, 1)),
            ],
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 1, 31));

        Assert.Equal(6m, Assert.Single(monthly).Expense);
    }

    [Fact]
    public void MonthlyTotals_is_empty_when_range_is_inverted()
    {
        Assert.Empty(DashboardCalculator.MonthlyTotals([], DateOnly.MaxValue, DateOnly.MinValue));
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
            Income(9999m, majorCategory: "Income"),
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

    private static Transaction Income(decimal amount, DateOnly? date = null, string category = "薪水", string? majorCategory = "Income") =>
        New(TransactionType.Income, amount, date, category, majorCategory);

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
