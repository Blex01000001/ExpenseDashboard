using System.Globalization;

namespace ExpenseDashboard.Web.Components;

/// <summary>
/// 畫面上的金額、筆數、百分比格式。一律 InvariantCulture（千分位逗號），不受伺服器文化影響。
/// 百分比不用 "P0"：部分文化會在數字和 % 之間插空白。
/// </summary>
public static class Display
{
    /// <summary>整數金額＋幣別：<c>12,345 TWD</c>。</summary>
    public static string Amount(decimal amount, string currency) => $"{Whole(amount)} {currency}";

    /// <summary>整數金額（不含幣別）：<c>12,345</c>。</summary>
    public static string Whole(decimal amount) => amount.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>單筆紀錄金額，保留小數：<c>1,234.5</c>。</summary>
    public static string Exact(decimal amount) => amount.ToString("#,0.##", CultureInfo.InvariantCulture);

    /// <summary>筆數：<c>1,234</c>。</summary>
    public static string Count(int count) => count.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>比例 → 百分比：0.123 → <c>12%</c>（decimals = 1 → <c>12.3%</c>）。</summary>
    public static string Percent(decimal ratio, int decimals = 0) =>
        (ratio * 100).ToString(decimals == 0 ? "0" : "0." + new string('0', decimals), CultureInfo.InvariantCulture) + "%";

    public static string Date(DateOnly date) => date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
}
