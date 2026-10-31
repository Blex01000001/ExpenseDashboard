using Bunit;
using ExpenseDashboard.Domain.Reports;
using ExpenseDashboard.Web.Components.Layout;
using ExpenseDashboard.Web.Components.Pages;

namespace ExpenseDashboard.Tests.Components;

public class SmallComponentTests : ComponentTestContext
{
    [Theory]
    [InlineData(null, "change-none", "—")]
    [InlineData("0.25", "change-up", "▲ 25%")]
    [InlineData("-0.1", "change-down", "▼ 10%")]
    [InlineData("0", "change-none", "持平")]
    public void ChangeBadge_shows_direction_with_symbol_and_color_class(string? ratio, string cssClass, string text)
    {
        var cut = Render<ChangeBadge>(p => p.Add(c => c.Ratio, ratio is null ? null : decimal.Parse(ratio)));

        var span = cut.Find("span");
        Assert.Equal(cssClass, span.ClassName);
        Assert.Equal(text, span.TextContent.Trim());
    }

    [Fact]
    public void MonthCard_shows_total_comparisons_daily_average_and_progress_badge()
    {
        var month = new MonthAnalysis(2026, 10, 12345m, 30, 823m, 0.1m, -0.2m,
            [new CategoryTotal("Diet", 12345m, 1m)]);

        var cut = Render<MonthCard>(p => p
            .Add(c => c.Month, month)
            .Add(c => c.Currency, "TWD")
            .Add(c => c.FeaturedMajorCategories, ["Diet"])
            .Add(c => c.InProgress, true));

        Assert.Contains("2026/10", cut.Find(".card-label").TextContent);
        Assert.NotNull(cut.Find(".badge"));
        Assert.Equal("12,345 TWD", cut.Find(".month-total").TextContent);
        Assert.Equal("▲ 10%", cut.Find(".change-up").TextContent.Trim());
        Assert.Equal("▼ 20%", cut.Find(".change-down").TextContent.Trim());
        Assert.Contains("823 TWD", cut.Find(".month-stats").TextContent);
    }

    [Fact]
    public void MonthCard_of_a_month_without_spending_says_so()
    {
        var month = new MonthAnalysis(2026, 2, 0m, 0, 0m, -1m, null, []);

        var cut = Render<MonthCard>(p => p
            .Add(c => c.Month, month)
            .Add(c => c.Currency, "TWD")
            .Add(c => c.FeaturedMajorCategories, []));

        Assert.Contains("這個月沒有支出", cut.Markup);
        Assert.Empty(cut.FindAll(".badge"));
    }

    [Fact]
    public void MainLayout_error_bar_and_navigation_are_in_chinese()
    {
        var cut = Render<MainLayout>(p => p.Add(c => c.Body, b => b.AddMarkupContent(0, "<p>內容</p>")));

        Assert.Contains("發生未預期的錯誤", cut.Find("#blazor-error-ui").TextContent);
        Assert.Equal("重新載入", cut.Find("#blazor-error-ui .reload").TextContent);
        Assert.Equal(
            ["儀表板", "匯入 CSV", "備註關鍵字", "標籤彙總"],
            cut.FindAll(".fab-label").Select(e => e.TextContent));
        Assert.Contains("內容", cut.Find("main").TextContent);
    }
}
