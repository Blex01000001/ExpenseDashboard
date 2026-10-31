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

    [Theory]
    [InlineData("BA", "BA 課金", true)]
    [InlineData("BA", "BA課金", true)]
    [InlineData("BA", "買BA石頭", true)]
    [InlineData("BA", "(ba)", true)]
    [InlineData("BA", "linebank 轉帳", false)]
    [InlineData("BA", "BAND 練團", false)]
    [InlineData("BA", "OBA", false)]
    [InlineData("GPT", "ChatGPT Plus", false)]
    [InlineData("*GPT", "ChatGPT Plus", true)]
    [InlineData("BA*", "BAND 練團", true)]
    [InlineData("*ba*", "linebank", true)]
    [InlineData("CLAUDE CODE", "Claude Code 月費", true)]
    [InlineData("7-11", "去7-11買東西", true)]
    [InlineData("聚餐", "跟朋友聚餐", true)]
    public void English_and_digit_keywords_match_whole_words_only(string keyword, string note, bool expected)
    {
        var rule = new KeywordRule { Name = "測試", Keywords = [keyword] };

        Assert.Equal(expected, rule.Matches(note));
    }

    [Fact]
    public void Later_whole_word_occurrence_still_matches_after_a_partial_one()
    {
        var rule = new KeywordRule { Name = "測試", Keywords = ["BA"] };

        Assert.Equal((10, 2), rule.FindMatch("linebank, BA 課金"));
    }

    [Fact]
    public void FindMatch_returns_the_earliest_match_across_keywords()
    {
        var rule = new KeywordRule { Name = "測試", Keywords = ["Steam", "BA"] };

        Assert.Equal((0, 2), rule.FindMatch("BA 與 Steam"));
        Assert.Null(rule.FindMatch("linebank"));
        Assert.Null(rule.FindMatch(null));
    }

    [Fact]
    public void Validate_rejects_wildcard_only_keywords()
    {
        Assert.Single(KeywordRule.Validate("測試", ["*"]));
        Assert.Single(KeywordRule.Validate("測試", ["BA", "**"]));
    }

    [Fact]
    public void Validate_requires_name_and_keywords()
    {
        Assert.Empty(KeywordRule.Validate("遊戲", ["Steam"]));
        Assert.Equal(2, KeywordRule.Validate("  ", []).Count);
        Assert.Single(KeywordRule.Validate(new string('字', KeywordRule.MaxNameLength + 1), ["x"]));
    }
}
