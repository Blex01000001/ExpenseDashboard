using ExpenseDashboard.Domain.Transactions;

namespace ExpenseDashboard.Tests;

/// <summary>
/// 測試用交易：只指定測試在意的欄位，其餘用固定預設值（每次新的 ExternalId）。
/// 各測試檔的小工具（Expense、Income…）都建立在這裡之上。
/// </summary>
internal static class TestTransactions
{
    public static readonly DateOnly DefaultDate = new(2025, 1, 4);
    public static readonly DateTime DefaultUpdatedAt = new(2025, 1, 4, 12, 0, 0);

    public static Transaction Create(
        decimal amount = 100m,
        DateOnly? date = null,
        string category = "午餐",
        string? majorCategory = "Diet",
        string? note = null,
        string? tags = null,
        string currency = "TWD",
        TransactionType type = TransactionType.Expense,
        Guid? externalId = null,
        DateTime? updatedAt = null) => new()
    {
        ExternalId = externalId ?? Guid.NewGuid(),
        Date = date ?? DefaultDate,
        Category = category,
        MajorCategory = majorCategory,
        Amount = amount,
        Currency = currency,
        Member = "自己",
        Account = "現金",
        Note = note,
        Tags = tags,
        Type = type,
        UpdatedAt = updatedAt ?? DefaultUpdatedAt,
    };
}
