using System.Globalization;
using ExpenseDashboard.Domain.Transactions;

namespace ExpenseDashboard.Domain.Import;

public sealed record CsvImportError(int LineNumber, string Message);

public sealed record CsvParseResult(IReadOnlyList<Transaction> Transactions, IReadOnlyList<CsvImportError> Errors);

/// <summary>
/// 解析「天天記帳」匯出的 CSV。欄位依標題名稱對應，欄位順序不影響結果。
/// 有問題的列會略過並記錄在 <see cref="CsvParseResult.Errors"/>，其餘列照常回傳。
/// </summary>
public static class DailyExpenseCsvParser
{
    private const string DateColumn = "日期";
    private const string CategoryColumn = "類別";
    private const string MajorCategoryColumn = "大類別";
    private const string AmountColumn = "金額";
    private const string CurrencyColumn = "貨幣";
    private const string MemberColumn = "成員";
    private const string AccountColumn = "帳戶";
    private const string NoteColumn = "備註";
    private const string TypeColumn = "收支區分";
    private const string UpdatedAtColumn = "上次更新";
    private const string UuidColumn = "UUID";

    // 選填：較新版本的匯出才有。
    private const string TagsColumn = "標籤";

    // 不同版本的「天天記帳」匯出用了不同欄名，統一對應到上面的名稱。
    private static readonly Dictionary<string, string> ColumnAliases = new(StringComparer.Ordinal)
    {
        ["幣別"] = CurrencyColumn,
    };

    private static readonly string[] RequiredColumns =
    [
        DateColumn, CategoryColumn, MajorCategoryColumn, AmountColumn, CurrencyColumn, MemberColumn,
        AccountColumn, NoteColumn, TypeColumn, UpdatedAtColumn, UuidColumn,
    ];

    public static CsvParseResult Parse(TextReader reader)
    {
        var transactions = new List<Transaction>();
        var errors = new List<CsvImportError>();

        using var records = CsvReader.Read(reader).GetEnumerator();
        if (!records.MoveNext())
        {
            errors.Add(new CsvImportError(1, "檔案是空的。"));
            return new CsvParseResult(transactions, errors);
        }

        var header = records.Current;
        var columns = MapColumns(header.Fields);
        var missing = RequiredColumns.Where(name => !columns.ContainsKey(name)).ToList();
        if (missing.Count > 0)
        {
            errors.Add(new CsvImportError(header.LineNumber, $"缺少欄位：{string.Join("、", missing)}。"));
            return new CsvParseResult(transactions, errors);
        }

        while (records.MoveNext())
        {
            var record = records.Current;
            if (record.Fields.Count != header.Fields.Count)
            {
                errors.Add(new CsvImportError(record.LineNumber,
                    $"欄位數量為 {record.Fields.Count}，應為 {header.Fields.Count}。"));
                continue;
            }

            var rowErrors = new List<string>();
            var transaction = ParseRow(
                name => columns.TryGetValue(name, out var index) ? record.Fields[index] : string.Empty,
                rowErrors);
            if (rowErrors.Count > 0)
            {
                errors.Add(new CsvImportError(record.LineNumber, string.Join(" ", rowErrors)));
            }
            else
            {
                transactions.Add(transaction);
            }
        }

        return new CsvParseResult(transactions, errors);
    }

    private static Dictionary<string, int> MapColumns(IReadOnlyList<string> headerFields)
    {
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < headerFields.Count; i++)
        {
            // 若呼叫端沒有用 StreamReader 去掉 BOM，第一個標題會以 U+FEFF 開頭。
            var name = headerFields[i].Trim().TrimStart('\uFEFF');
            columns.TryAdd(ColumnAliases.GetValueOrDefault(name, name), i);
        }
        return columns;
    }

    private static Transaction ParseRow(Func<string, string> field, List<string> errors)
    {
        var transaction = new Transaction
        {
            Category = field(CategoryColumn).Trim(),
            MajorCategory = NullIfEmpty(field(MajorCategoryColumn)),
            Currency = field(CurrencyColumn).Trim().ToUpperInvariant(),
            Member = field(MemberColumn).Trim(),
            Account = field(AccountColumn).Trim(),
            Note = NullIfEmpty(field(NoteColumn)),
            Tags = NullIfEmpty(field(TagsColumn)),
        };

        var date = field(DateColumn).Trim();
        if (DateOnly.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            transaction.Date = parsedDate;
        }
        else
        {
            errors.Add($"日期「{date}」格式錯誤，應為 yyyyMMdd。");
        }

        var amount = field(AmountColumn).Trim();
        if (decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount) && parsedAmount >= 0)
        {
            transaction.Amount = parsedAmount;
        }
        else
        {
            errors.Add($"金額「{amount}」不是有效的非負數字。");
        }

        if (transaction.Currency.Length == 0)
        {
            errors.Add("貨幣不可為空。");
        }

        var type = field(TypeColumn).Trim();
        switch (type)
        {
            case "支":
                transaction.Type = TransactionType.Expense;
                break;
            case "收":
                transaction.Type = TransactionType.Income;
                break;
            default:
                errors.Add($"收支區分「{type}」無法辨識，應為「支」或「收」。");
                break;
        }

        var updatedAt = field(UpdatedAtColumn).Trim();
        if (DateTime.TryParseExact(updatedAt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedUpdatedAt))
        {
            transaction.UpdatedAt = parsedUpdatedAt;
        }
        else
        {
            errors.Add($"上次更新「{updatedAt}」格式錯誤，應為 yyyy-MM-dd HH:mm:ss。");
        }

        var uuid = field(UuidColumn).Trim();
        if (Guid.TryParse(uuid, out var parsedUuid))
        {
            transaction.ExternalId = parsedUuid;
        }
        else
        {
            errors.Add($"UUID「{uuid}」格式錯誤。");
        }

        return transaction;
    }

    private static string? NullIfEmpty(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
