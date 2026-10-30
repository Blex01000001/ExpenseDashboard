using System.Text;

namespace ExpenseDashboard.Domain.Import;

/// <summary>一列 CSV 紀錄；<see cref="LineNumber"/> 為該列在檔案中的起始行號（從 1 起算）。</summary>
public sealed record CsvRecord(int LineNumber, IReadOnlyList<string> Fields);

/// <summary>
/// 符合 RFC 4180 的最小 CSV 讀取器：支援以雙引號包住的欄位、欄位內的逗號與換行、
/// 以及用兩個雙引號跳脫的引號。
/// </summary>
public static class CsvReader
{
    public static IEnumerable<CsvRecord> Read(TextReader reader)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordStartLine = 1;
        var recordHasContent = false;

        int c;
        while ((c = reader.Read()) != -1)
        {
            var ch = (char)c;

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (ch == '\n')
                    {
                        line++;
                    }
                    field.Append(ch);
                }
                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    recordHasContent = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    recordHasContent = true;
                    break;
                case '\r':
                    if (reader.Peek() == '\n')
                    {
                        reader.Read();
                    }
                    goto case '\n';
                case '\n':
                    if (recordHasContent)
                    {
                        fields.Add(field.ToString());
                        yield return new CsvRecord(recordStartLine, fields.ToArray());
                    }
                    fields.Clear();
                    field.Clear();
                    recordHasContent = false;
                    line++;
                    recordStartLine = line;
                    break;
                default:
                    field.Append(ch);
                    recordHasContent = true;
                    break;
            }
        }

        if (recordHasContent)
        {
            fields.Add(field.ToString());
            yield return new CsvRecord(recordStartLine, fields.ToArray());
        }
    }
}
