namespace ExpenseDashboard.Tests;

/// <summary>固定「今天」的 <see cref="TimeProvider"/>（中午 12 點、UTC），讓測試結果不隨執行日期改變。</summary>
public sealed class FixedTimeProvider(DateOnly today) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
}
