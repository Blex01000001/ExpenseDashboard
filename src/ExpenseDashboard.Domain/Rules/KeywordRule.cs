namespace ExpenseDashboard.Domain.Rules;

/// <summary>
/// 備註關鍵字規則：備註符合任一關鍵字（不分大小寫）的支出，算進這個自訂項目。
/// 只在統計時比對，不寫回交易資料，所以修改規則會立刻套用到全部歷史資料。
/// <para>
/// 比對方式：關鍵字開頭或結尾是英文字母／數字時，該側必須是「字的邊界」——
/// 前後不能緊接著其他英文字母或數字，所以 <c>BA</c> 符合「BA 課金」「BA課金」，但不符合「linebank」。
/// 中文等其他字元沒有字的邊界，照樣用「包含」比對。
/// 在關鍵字前或後加 <c>*</c> 可以取消該側的邊界限制，例如 <c>*GPT</c> 符合「ChatGPT」。
/// </para>
/// </summary>
public class KeywordRule
{
    public const int MaxNameLength = 50;

    public const char Wildcard = '*';

    private static readonly char[] KeywordSeparators = ['\n', '\r', ',', '，', '、'];

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<string> Keywords { get; set; } = [];

    public bool Matches(string? note) => FindMatch(note) is not null;

    /// <summary>備註中第一個符合的位置（給畫面標示用）；不符合時為 null。</summary>
    public (int Index, int Length)? FindMatch(string? note)
    {
        if (string.IsNullOrEmpty(note))
        {
            return null;
        }

        (int Index, int Length)? first = null;
        foreach (var keyword in Keywords)
        {
            if (FindKeyword(note, keyword) is { } match && (first is null || match.Index < first.Value.Index))
            {
                first = match;
            }
        }
        return first;
    }

    private static (int Index, int Length)? FindKeyword(string note, string keyword)
    {
        var openStart = keyword.StartsWith(Wildcard);
        var openEnd = keyword.EndsWith(Wildcard);
        var core = keyword.Trim(Wildcard);
        if (core.Length == 0)
        {
            return null;
        }

        for (var index = note.IndexOf(core, StringComparison.OrdinalIgnoreCase);
             index >= 0;
             index = note.IndexOf(core, index + 1, StringComparison.OrdinalIgnoreCase))
        {
            var end = index + core.Length;
            var startOk = openStart || !IsWordChar(core[0]) || index == 0 || !IsWordChar(note[index - 1]);
            var endOk = openEnd || !IsWordChar(core[^1]) || end == note.Length || !IsWordChar(note[end]);
            if (startOk && endOk)
            {
                return (index, core.Length);
            }
        }
        return null;
    }

    private static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c);

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
        else if (keywords.Any(k => k.Trim(Wildcard).Length == 0))
        {
            errors.Add($"關鍵字不能只有「{Wildcard}」。");
        }
        return errors;
    }
}
