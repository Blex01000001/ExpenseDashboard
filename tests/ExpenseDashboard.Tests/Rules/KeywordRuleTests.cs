using ExpenseDashboard.Domain.Rules;

namespace ExpenseDashboard.Tests.Rules;

public class KeywordRuleTests
{
    [Fact]
    public void ParseKeywords_splits_on_newlines_commas_and_enumeration_commas()
    {
        var keywords = KeywordRule.ParseKeywords("Steam\r\nBA, 原神，Switch、  ");

        Assert.Equal(["Steam", "BA", "原神", "Switch"], keywords);
    }

    [Fact]
    public void ParseKeywords_removes_case_insensitive_duplicates_and_handles_null()
    {
        Assert.Equal(["Steam"], KeywordRule.ParseKeywords("Steam\nsteam\nSTEAM"));
        Assert.Empty(KeywordRule.ParseKeywords(null));
    }

    [Theory]
    [InlineData("Steam 特價", true)]
    [InlineData("買了 steam 卡", true)]
    [InlineData("ba", true)]
    [InlineData("晚餐", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Matches_any_keyword_ignoring_case(string? note, bool expected)
    {
        var rule = new KeywordRule { Name = "遊戲", Keywords = ["STEAM", "BA"] };

        Assert.Equal(expected, rule.Matches(note));
    }

    [Fact]
    public void Validate_requires_name_and_keywords()
    {
        Assert.Empty(KeywordRule.Validate("遊戲", ["Steam"]));
        Assert.Equal(2, KeywordRule.Validate("  ", []).Count);
        Assert.Single(KeywordRule.Validate(new string('字', KeywordRule.MaxNameLength + 1), ["x"]));
    }
}
