using Bunit;
using ExpenseDashboard.Domain.Reports;
using ExpenseDashboard.Web.Components.Pages;

namespace ExpenseDashboard.Tests.Components;

public class DashboardPageTests : ComponentTestContext
{
    [Fact]
    public void Home_without_data_points_to_the_import_page()
    {
        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("尚無資料", cut.Markup));
        Assert.Equal("import", cut.Find("main a, p a").GetAttribute("href"));
    }

    [Fact]
    public async Task Home_shows_year_card_with_year_over_year_and_month_cards_newest_first()
    {
        await SeedAsync(
            Expense(new DateOnly(2025, 3, 10), 100m),
            Expense(new DateOnly(2026, 2, 1), 300m),
            Expense(new DateOnly(2026, 10, 1), 200m));

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".year-card")));
        var yearCard = cut.Find(".year-card");
        Assert.Contains("2026 年度花費", yearCard.TextContent);
        Assert.Equal("500 TWD", cut.Find(".hero-value").TextContent);
        Assert.Equal("▲ 400%", yearCard.QuerySelector(".change-up")!.TextContent.Trim());
        Assert.Contains("2025 年 1/1–10/15", yearCard.TextContent);
        Assert.Equal(
            ["2026/10", "2026/09", "2026/08", "2026/07", "2026/06", "2026/05", "2026/04", "2026/03", "2026/02"],
            cut.FindAll(".month-card .card-label").Select(e => e.TextContent.Replace("進行中", "").Trim()));
        Assert.Single(cut.FindAll(".month-card .badge"));
    }

    [Fact]
    public async Task Changing_the_year_reloads_the_cards()
    {
        await SeedAsync(Expense(new DateOnly(2025, 3, 10), 100m), Expense(new DateOnly(2026, 2, 1), 300m));
        var cut = Render<Home>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".year-card")));

        cut.Find("select[aria-label=年度]").Change("2025");

        cut.WaitForAssertion(() => Assert.Contains("2025 年度花費", cut.Find(".year-card").TextContent));
        Assert.Equal(["2025/03"], cut.FindAll(".month-card .card-label").Select(e => e.TextContent.Trim()));
    }

    [Fact]
    public void Trend_card_starts_with_default_items_and_adds_and_removes_lines()
    {
        TrendItem Item(TrendItemKind kind, string name, decimal total) =>
            new(kind, name, total, Enumerable.Repeat<decimal?>(1m, 12).ToArray());
        IReadOnlyList<TrendItem> items =
        [
            Item(TrendItemKind.MajorCategory, "Diet", 900m),
            Item(TrendItemKind.Category, "晚餐", 500m),
            Item(TrendItemKind.Category, "午餐", 400m),
            new TrendItem(TrendItemKind.Keyword, "AI 服務", 50m, Enumerable.Repeat<decimal?>(0m, 12).ToArray()) { RuleId = 3 },
        ];

        var cut = Render<MonthlyTrendCard>(p => p
            .Add(c => c.MonthlyTotals, Enumerable.Range(1, 12).Select(m => new MonthValue(m, 10m)).ToList())
            .Add(c => c.Items, items)
            .Add(c => c.DefaultItems, DashboardCalculator.DefaultTrendItems(items))
            .Add(c => c.ChartKeyPrefix, "2026-TWD"));

        cut.WaitForAssertion(() => Assert.Equal(["晚餐", "午餐"], Chips(cut)));

        cut.Find(".trend-picker select").Change("k:3");
        Assert.Equal(["晚餐", "午餐", "AI 服務（備註）"], Chips(cut));
        Assert.Contains(cut.FindAll(".trend-picker optgroup"), g => g.GetAttribute("label") == "備註關鍵字");

        cut.FindAll(".chip-remove")[0].Click();
        Assert.Equal(["午餐", "AI 服務（備註）"], Chips(cut));
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "localStorage.setItem");
    }

    private static List<string> Chips(IRenderedComponent<MonthlyTrendCard> cut) =>
        cut.FindAll(".chip").Select(c => c.TextContent.Replace("×", "").Trim()).ToList();
}
