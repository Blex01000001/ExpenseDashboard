using Bunit;
using ExpenseDashboard.Web.Components.Pages;
using TagsPage = ExpenseDashboard.Web.Components.Pages.Tags;

namespace ExpenseDashboard.Tests.Components;

public class SettingsPageTests : ComponentTestContext
{
    [Fact]
    public async Task Keyword_rule_can_be_added_expanded_and_deleted()
    {
        await SeedAsync(
            Expense(new DateOnly(2026, 10, 1), 300m, "ゲーム", null, note: "BA 課金"),
            Expense(new DateOnly(2026, 10, 2), 5000m, "轉帳", null, note: "linebank 轉帳"));
        var cut = Render<KeywordRules>();
        cut.WaitForAssertion(() => Assert.Contains("還沒有任何規則", cut.Markup));

        cut.Find(".rule-form .btn-primary").Click();
        cut.WaitForAssertion(() => Assert.Contains("請輸入名稱", cut.Find(".form-errors").TextContent));

        cut.Find(".rule-form input").Change("遊戲");
        cut.Find(".rule-form textarea").Change("BA");
        cut.Find(".rule-form .btn-primary").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".rule-row")));
        Assert.Contains("300", cut.Find(".rule-row").TextContent);

        cut.Find(".rule-row td:nth-child(2)").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".match-table tbody tr")));
        Assert.Equal("BA", cut.Find(".match-table mark").TextContent);

        var delete = cut.Find(".rule-row .btn-danger");
        Assert.Equal("刪除", delete.TextContent);
        delete.Click();
        cut.Find(".rule-row .btn-danger").Click();   // 「確定刪除」
        cut.WaitForAssertion(() => Assert.Contains("還沒有任何規則", cut.Markup));
    }

    [Fact]
    public async Task Rule_list_comes_before_the_form()
    {
        await SeedAsync(Expense(new DateOnly(2026, 10, 1), 300m, note: "x"));
        var cut = Render<KeywordRules>();
        cut.Find(".rule-form input").Change("測試");
        cut.Find(".rule-form textarea").Change("x");
        cut.Find(".rule-form .btn-primary").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".rule-row")));

        var markup = cut.Markup;
        Assert.True(markup.IndexOf("rule-table", StringComparison.Ordinal) < markup.IndexOf("rule-form", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tags_page_without_tags_explains_how_to_get_them()
    {
        await SeedAsync(Expense(new DateOnly(2026, 10, 1), 300m));

        var cut = Render<TagsPage>();

        cut.WaitForAssertion(() => Assert.Contains("目前匯入的紀錄都沒有標籤", cut.Markup));
        Assert.Empty(cut.FindAll("select"));
    }

    [Fact]
    public async Task Tags_page_lists_tags_and_expands_monthly_amounts()
    {
        await SeedAsync(
            Expense(new DateOnly(2026, 3, 1), 300m, tags: "旅行, 工作"),
            Expense(new DateOnly(2026, 4, 1), 100m, tags: "旅行"),
            Expense(new DateOnly(2026, 4, 2), 600m));

        var cut = Render<TagsPage>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".rule-row").Count));
        Assert.Contains("旅行", cut.FindAll(".rule-row")[0].TextContent);
        Assert.Contains("40.0%", cut.FindAll(".rule-row")[0].TextContent);
        Assert.Contains("3 筆，其中 2 筆有標籤", cut.Markup);

        cut.FindAll(".rule-row")[0].Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".match-table tbody tr").Count));
        var months = cut.FindAll(".tag-month").Select(e => string.Concat(e.TextContent.Where(c => !char.IsWhiteSpace(c)))).ToList();
        Assert.Equal("3月300", months[2]);
        Assert.Equal("4月100", months[3]);
        Assert.Equal("11月—", months[10]);
    }
}
