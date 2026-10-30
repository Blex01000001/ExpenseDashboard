using ExpenseDashboard.Domain.Import;

namespace ExpenseDashboard.Tests.Import;

public class CsvReaderTests
{
    private static List<CsvRecord> Read(string csv) => CsvReader.Read(new StringReader(csv)).ToList();

    [Fact]
    public void Splits_plain_fields_and_records()
    {
        var records = Read("a,b,c\n1,2,3\n");

        Assert.Equal(2, records.Count);
        Assert.Equal(["a", "b", "c"], records[0].Fields);
        Assert.Equal(["1", "2", "3"], records[1].Fields);
    }

    [Fact]
    public void Keeps_empty_fields_including_trailing_one()
    {
        var record = Assert.Single(Read("a,,c,"));

        Assert.Equal(["a", "", "c", ""], record.Fields);
    }

    [Fact]
    public void Quoted_field_may_contain_commas_escaped_quotes_and_newlines()
    {
        var record = Assert.Single(Read("x,\"便當, 加蛋\",\"他說\"\"好\"\"\",\"第一行\n第二行\"\n"));

        Assert.Equal(["x", "便當, 加蛋", "他說\"好\"", "第一行\n第二行"], record.Fields);
    }

    [Fact]
    public void Handles_crlf_line_endings_and_missing_final_newline()
    {
        var records = Read("a,b\r\n1,2");

        Assert.Equal(2, records.Count);
        Assert.Equal(["1", "2"], records[1].Fields);
    }

    [Fact]
    public void Skips_blank_lines()
    {
        var records = Read("a\n\n\r\nb\n");

        Assert.Equal(["a", "b"], records.Select(r => r.Fields[0]));
    }

    [Fact]
    public void Line_numbers_point_at_the_start_of_each_record()
    {
        var records = Read("h\n\"多\n行\"\n\nlast\n");

        Assert.Equal([1, 2, 5], records.Select(r => r.LineNumber));
    }
}
