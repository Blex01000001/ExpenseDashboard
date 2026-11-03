using ApexCharts;

namespace ExpenseDashboard.Web.Components;

/// <summary>
/// 所有 ApexChart 共用的色票、JS 格式化字串與基本設定。
/// 色票已用 dataviz 驗證工具驗過對 <see cref="Surface"/> 的對比與色盲區分，換色前要重新驗證；
/// 與 app.css 的 :root token 保持一致。
/// </summary>
public static class ChartTheme
{
    public const string Surface = "#1a1a19";
    public const string Grid = "#2c2c2a";
    public const string MutedInk = "#898781";
    /// <summary>支出橘（app.css --expense）。</summary>
    public const string Expense = "#d95926";

    /// <summary>折線色（跳過支出橘），依欄位固定順序配色。</summary>
    public static readonly IReadOnlyList<string> LineColors = ["#3987e5", "#199e70", "#c98500", "#d55181", "#008300"];
    /// <summary>超過 3 條線時色盲難以分辨，用虛線樣式當第二種編碼；與 <see cref="LineColors"/> 一一對應。</summary>
    public static readonly IReadOnlyList<int> LineDashes = [0, 6, 2, 10, 4];
    /// <summary>甜甜圈扇形色，依序配給年度前 5 名大類別。</summary>
    public static readonly IReadOnlyList<string> SliceColors = ["#3987e5", Expense, "#199e70", "#c98500", "#d55181"];
    /// <summary>甜甜圈「其他」。</summary>
    public const string Other = MutedInk;

    // Interactive Server 不支援 C# 委派格式化，一律用 JS 字串。
    public const string AmountFormatter = "function (value) { return value == null ? '—' : Number(value).toLocaleString('zh-TW', { maximumFractionDigits: 2 }); }";
    public const string WholeAmountFormatter = "function (value) { return Number(value).toLocaleString('zh-TW', { maximumFractionDigits: 0 }); }";
    public const string CompactAxisFormatter = "function (value) { return Number(value).toLocaleString('zh-TW', { notation: 'compact', maximumFractionDigits: 1 }); }";
    public const string DonutTotalFormatter = "function (w) { return w.globals.seriesTotals.reduce(function (a, b) { return a + b; }, 0).toLocaleString('zh-TW', { maximumFractionDigits: 0 }); }";
    /// <summary>窄螢幕的月份標籤：「3 月」→「3」。</summary>
    public const string MonthNumberFormatter = "function (value) { return String(value).replace(' 月', ''); }";

    /// <summary>暗色、透明背景、無工具列的 Chart 設定；每次呼叫回傳新實例（options 不能共用）。</summary>
    public static Chart DarkChart() => new()
    {
        Toolbar = new Toolbar { Show = false },
        Zoom = new Zoom { Enabled = false },
        FontFamily = "inherit",
        Background = "transparent",
        ForeColor = MutedInk,
    };

    public static Theme DarkTheme() => new() { Mode = Mode.Dark };
}
