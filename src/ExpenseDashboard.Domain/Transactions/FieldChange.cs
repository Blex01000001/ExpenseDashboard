namespace ExpenseDashboard.Domain.Transactions;

/// <summary>一個欄位的變動（顯示用文字），例如「金額：200 → 250」。</summary>
public sealed record FieldChange(string Field, string Before, string After);
