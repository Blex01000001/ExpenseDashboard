using System.Text;
using ExpenseDashboard.Domain.Import;
using ExpenseDashboard.Domain.Transactions;

namespace ExpenseDashboard.Tests.Import;

public class DailyExpenseCsvParserTests
{
    private const string Header = "日期,類別,大類別,金額,貨幣,成員,帳戶,備註,收支區分,上次更新,UUID";
    private const string ValidRow = "20250104,午餐,Diet ,120,TWD,自己,現金,,支,2025-01-04 12:30:15,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9";

    private static CsvParseResult Parse(string csv) => DailyExpenseCsvParser.Parse(new StringReader(csv));

    private static CsvParseResult ParseSampleExport(string fileName = "sample-export.csv")
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return DailyExpenseCsvParser.Parse(reader);
    }

    [Fact]
    public void Sample_export_parses_every_row_without_errors()
    {
        var result = ParseSampleExport();

        Assert.Empty(result.Errors);
        Assert.Equal(5, result.Transactions.Count);
    }

    [Fact]
    public void Sample_export_maps_all_columns()
    {
        var lunch = ParseSampleExport().Transactions
            .Single(t => t.ExternalId == Guid.Parse("1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9"));

        Assert.Equal(new DateOnly(2025, 1, 4), lunch.Date);
        Assert.Equal("午餐", lunch.Category);
        Assert.Equal("Diet", lunch.MajorCategory);
        Assert.Equal(120m, lunch.Amount);
        Assert.Equal("TWD", lunch.Currency);
        Assert.Equal("自己", lunch.Member);
        Assert.Equal("現金", lunch.Account);
        Assert.Equal("便當, 加蛋", lunch.Note);
        Assert.Equal(TransactionType.Expense, lunch.Type);
        Assert.Equal(new DateTime(2025, 1, 4, 12, 30, 15), lunch.UpdatedAt);
    }

    [Fact]
    public void Sample_export_handles_income_empty_major_category_and_escaped_quotes()
    {
        var transactions = ParseSampleExport().Transactions;

        Assert.Equal(TransactionType.Income, transactions.Single(t => t.Category == "薪水").Type);
        Assert.Null(transactions.Single(t => t.Category == "運動").MajorCategory);
        Assert.Equal("他說\"好書\"", transactions.Single(t => t.Category == "書籍").Note);
    }

    [Fact]
    public void Newer_export_with_currency_alias_and_tags_column_parses()
    {
        var result = ParseSampleExport("sample-export-v2.csv");

        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Transactions.Count);
        Assert.All(result.Transactions, t => Assert.Equal("TWD", t.Currency));

        var game = result.Transactions.Single(t => t.Category == "遊戲");
        Assert.Equal("休閒", game.Tags);
        Assert.Equal("Steam, 特價", game.Note);
        Assert.Null(game.MajorCategory);
        Assert.Equal(300m, game.Amount);

        Assert.Equal("工作, 外出", result.Transactions.Single(t => t.Category == "咖啡").Tags);
        Assert.Null(result.Transactions.Single(t => t.Category == "晚餐").Tags);
    }

    [Fact]
    public void Export_without_tags_column_leaves_tags_null()
    {
        Assert.All(ParseSampleExport().Transactions, t => Assert.Null(t.Tags));
    }

    [Fact]
    public void Missing_currency_is_reported_by_its_canonical_name()
    {
        var csv = "日期,類別,大類別,金額,成員,帳戶,備註,收支區分,上次更新,UUID\n";

        var error = Assert.Single(Parse(csv).Errors);

        Assert.Contains("貨幣", error.Message);
    }

    [Fact]
    public void Empty_note_becomes_null()
    {
        var transaction = Assert.Single(Parse($"{Header}\n{ValidRow}\n").Transactions);

        Assert.Null(transaction.Note);
    }

    [Fact]
    public void Leading_bom_in_header_is_ignored()
    {
        var result = Parse($"﻿{Header}\n{ValidRow}\n");

        Assert.Empty(result.Errors);
        Assert.Single(result.Transactions);
    }

    [Fact]
    public void Columns_are_matched_by_header_name_not_position()
    {
        var csv = """
            UUID,收支區分,金額,日期,類別,大類別,貨幣,成員,帳戶,備註,上次更新
            1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9,收,1000,20250105,獎金,,TWD,自己,銀行,,2025-01-05 09:00:00
            """;

        var transaction = Assert.Single(Parse(csv).Transactions);

        Assert.Equal(TransactionType.Income, transaction.Type);
        Assert.Equal(1000m, transaction.Amount);
        Assert.Equal("獎金", transaction.Category);
        Assert.Equal(new DateOnly(2025, 1, 5), transaction.Date);
    }

    [Fact]
    public void Decimal_and_thousands_separated_amounts_are_accepted()
    {
        var csv = $"{Header}\n" +
                  "20250104,午餐,Diet,12.5,USD,自己,現金,,支,2025-01-04 12:30:15,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9\n" +
                  "20250104,房租,Home,\"12,000\",TWD,自己,銀行,,支,2025-01-04 12:30:15,2B3C4D5E-6F70-4182-93A4-B5C6D7E8F901\n";

        var result = Parse(csv);

        Assert.Empty(result.Errors);
        Assert.Equal([12.5m, 12000m], result.Transactions.Select(t => t.Amount));
    }

    [Fact]
    public void Missing_columns_are_reported_and_nothing_is_parsed()
    {
        var result = Parse("日期,類別,金額\n20250104,午餐,120\n");

        Assert.Empty(result.Transactions);
        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
        Assert.Contains("大類別", error.Message);
        Assert.Contains("UUID", error.Message);
    }

    [Fact]
    public void Empty_input_is_reported()
    {
        var result = Parse("");

        Assert.Empty(result.Transactions);
        Assert.Single(result.Errors);
    }

    [Theory]
    [InlineData("2025/01/04,午餐,Diet,120,TWD,自己,現金,,支,2025-01-04 12:30:15,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9", "日期")]
    [InlineData("20250104,午餐,Diet,abc,TWD,自己,現金,,支,2025-01-04 12:30:15,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9", "金額")]
    [InlineData("20250104,午餐,Diet,-5,TWD,自己,現金,,支,2025-01-04 12:30:15,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9", "金額")]
    [InlineData("20250104,午餐,Diet,120,,自己,現金,,支,2025-01-04 12:30:15,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9", "貨幣")]
    [InlineData("20250104,午餐,Diet,120,TWD,自己,現金,,轉帳,2025-01-04 12:30:15,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9", "收支區分")]
    [InlineData("20250104,午餐,Diet,120,TWD,自己,現金,,支,2025/01/04,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9", "上次更新")]
    [InlineData("20250104,午餐,Diet,120,TWD,自己,現金,,支,2025-01-04 12:30:15,not-a-uuid", "UUID")]
    [InlineData("20250104,午餐,Diet,120,TWD", "欄位數量")]
    public void Invalid_row_is_skipped_with_line_number_and_reason(string badRow, string expectedInMessage)
    {
        var result = Parse($"{Header}\n{ValidRow}\n{badRow}\n");

        Assert.Single(result.Transactions);
        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Contains(expectedInMessage, error.Message);
    }

    [Fact]
    public void Row_with_several_problems_reports_them_together()
    {
        var result = Parse($"{Header}\nbad,午餐,Diet,abc,TWD,自己,現金,,?,x,y\n");

        var error = Assert.Single(result.Errors);
        Assert.Contains("日期", error.Message);
        Assert.Contains("金額", error.Message);
        Assert.Contains("收支區分", error.Message);
        Assert.Contains("上次更新", error.Message);
        Assert.Contains("UUID", error.Message);
    }

    [Fact]
    public void Line_numbers_account_for_multiline_notes()
    {
        var csv = $"{Header}\n" +
                  "20250104,午餐,Diet,120,TWD,自己,現金,\"第一行\n第二行\",支,2025-01-04 12:30:15,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9\n" +
                  "20250104,午餐,Diet,120,TWD,自己,現金,,支,2025-01-04 12:30:15,bad\n";

        var result = Parse(csv);

        Assert.Equal("第一行\n第二行", Assert.Single(result.Transactions).Note);
        Assert.Equal(4, Assert.Single(result.Errors).LineNumber);
    }
}
