namespace ExpenseDashboard.Domain.Rules;

/// <summary>
/// 備註關鍵字規則：備註包含任一關鍵字（不分大小寫）的支出，算進這個自訂項目。
/// 只在統計時比對，不寫回交易資料，所以修改規則會立刻套用到全部歷史資料。
/// </summary>
public class KeywordRule
{
    public const int MaxNameLength = 50;

    private static readonly char[] KeywordSeparators = ['\n', '\r', ',', '，', '、'];

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<string> Keywords { get; set; } = [];

    public bool Matches(string? note) =>
        !string.IsNullOrEmpty(note)
        && Keywords.Any(k => note.Contains(k, StringComparison.OrdinalIgnoreCase));

    /// <summary>把使用者輸入（換行、逗號或頓號分隔）拆成去頭尾空白、不分大小寫去重的關鍵字。</summary>
    public static List<string> ParseKeywords(string? raw) =>
        (raw ?? string.Empty)
            .Split(KeywordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>驗證名稱與關鍵字，回傳錯誤訊息（沒有錯誤時為空）。名稱重複由資料層檢查。</summary>
    public static IReadOnlyList<string> Validate(string? name, IReadOnlyCollection<string> keywords)
    {
        var errors = new List<string>();
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            errors.Add("請輸入名稱。");
        }
        else if (trimmed.Length > MaxNameLength)
        {
            errors.Add($"名稱最多 {MaxNameLength} 個字。");
        }

        if (keywords.Count == 0)
        {
            errors.Add("請至少輸入一個關鍵字。");
        }
        return errors;
    }
}
