using System.Globalization;

namespace ExpenseDashboard.Domain.Transactions;

/// <summary>
/// 一筆收支紀錄，對應「天天記帳」CSV 匯出的一列。
/// </summary>
public class Transaction
{
    public int Id { get; set; }

    /// <summary>CSV「UUID」欄，用來在重複匯入時去重。</summary>
    public Guid ExternalId { get; set; }

    /// <summary>CSV「日期」欄（yyyyMMdd）。</summary>
    public DateOnly Date { get; set; }

    /// <summary>CSV「類別」欄，例如「晚餐」。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>CSV「大類別」欄，例如「Diet」；可能為空。</summary>
    public string? MajorCategory { get; set; }

    /// <summary>CSV「金額」欄，一律為正數，方向由 <see cref="Type"/> 決定。</summary>
    public decimal Amount { get; set; }

    /// <summary>CSV「貨幣」欄（ISO 4217，例如 TWD）。</summary>
    public string Currency { get; set; } = "TWD";

    /// <summary>CSV「成員」欄。</summary>
    public string Member { get; set; } = string.Empty;

    /// <summary>CSV「帳戶」欄。</summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>CSV「備註」欄。</summary>
    public string? Note { get; set; }

    /// <summary>CSV「標籤」欄（較新版本的匯出才有）。</summary>
    public string? Tags { get; set; }

    /// <summary>CSV「收支區分」欄。</summary>
    public TransactionType Type { get; set; }

    /// <summary>CSV「上次更新」欄（yyyy-MM-dd HH:mm:ss，本地時間）。</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 相對 <paramref name="previous"/>（資料庫裡的舊版本）有變動的欄位，依 CSV 欄位順序，值已轉成顯示用文字。
    /// 只差在「上次更新」時回傳空清單。
    /// </summary>
    public IReadOnlyList<FieldChange> ChangesFrom(Transaction previous)
    {
        var changes = new List<FieldChange>();
        void Compare<T>(string field, T before, T after, Func<T, string> format)
        {
            if (!EqualityComparer<T>.Default.Equals(before, after))
            {
                changes.Add(new FieldChange(field, format(before), format(after)));
            }
        }

        static string Text(string? value) => string.IsNullOrEmpty(value) ? "（空）" : value;

        Compare("日期", previous.Date, Date, d => d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
        Compare("類別", previous.Category, Category, Text);
        Compare("大類別", previous.MajorCategory, MajorCategory, Text);
        Compare("金額", previous.Amount, Amount, a => a.ToString("#,0.##", CultureInfo.InvariantCulture));
        Compare("貨幣", previous.Currency, Currency, Text);
        Compare("成員", previous.Member, Member, Text);
        Compare("帳戶", previous.Account, Account, Text);
        Compare("標籤", previous.Tags, Tags, Text);
        Compare("備註", previous.Note, Note, Text);
        Compare("收支區分", previous.Type, Type, t => t == TransactionType.Income ? "收" : "支");
        return changes;
    }

    /// <summary>
    /// 匯出內容（不含 <see cref="Id"/>、<see cref="ExternalId"/>、<see cref="UpdatedAt"/>）是否完全相同。
    /// 用來判斷「上次更新」沒變、內容卻變了的紀錄（例如 App 改了類別名稱）。
    /// </summary>
    public bool HasSameContent(Transaction other) =>
        Date == other.Date
        && Category == other.Category
        && MajorCategory == other.MajorCategory
        && Amount == other.Amount
        && Currency == other.Currency
        && Member == other.Member
        && Account == other.Account
        && Note == other.Note
        && Tags == other.Tags
        && Type == other.Type;
}
