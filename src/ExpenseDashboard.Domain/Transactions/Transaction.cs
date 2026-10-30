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
}
