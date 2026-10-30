namespace ExpenseDashboard.Domain.Transactions;

/// <summary>收支區分。CSV 中以「支」/「收」表示。</summary>
public enum TransactionType
{
    Expense = 0,
    Income = 1,
}
