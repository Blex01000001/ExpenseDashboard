using ExpenseDashboard.Domain.Transactions;
using ExpenseDashboard.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseDashboard.Web.Import;

public sealed record ImportSummary(int Added, int Updated, int Unchanged);

/// <summary>
/// 以 <see cref="Transaction.ExternalId"/> 做 upsert：新的 UUID 新增；
/// 已存在且 <see cref="Transaction.UpdatedAt"/> 較新的覆寫；其餘視為未變更。
/// 匯出檔裡沒有出現的既有資料不會被刪除。
/// </summary>
public class TransactionImporter(IDbContextFactory<ExpenseDbContext> dbFactory)
{
    // 分批查詢既有資料，避免 IN 子句的參數數量超過 SQLite 上限。
    private const int LookupBatchSize = 500;

    public async Task<ImportSummary> ImportAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        // 同一檔案內若有重複 UUID，只保留最後更新的那筆。
        var incoming = transactions
            .GroupBy(t => t.ExternalId)
            .Select(g => g.MaxBy(t => t.UpdatedAt)!)
            .ToList();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = new Dictionary<Guid, Transaction>();
        foreach (var chunk in incoming.Select(t => t.ExternalId).Chunk(LookupBatchSize))
        {
            var found = await db.Transactions
                .Where(t => chunk.Contains(t.ExternalId))
                .ToListAsync(cancellationToken);
            foreach (var transaction in found)
            {
                existing[transaction.ExternalId] = transaction;
            }
        }

        int added = 0, updated = 0, unchanged = 0;
        foreach (var transaction in incoming)
        {
            if (!existing.TryGetValue(transaction.ExternalId, out var current))
            {
                transaction.Id = 0;
                db.Transactions.Add(transaction);
                added++;
            }
            else if (transaction.UpdatedAt > current.UpdatedAt)
            {
                CopyValues(transaction, current);
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ImportSummary(added, updated, unchanged);
    }

    private static void CopyValues(Transaction source, Transaction target)
    {
        target.Date = source.Date;
        target.Category = source.Category;
        target.MajorCategory = source.MajorCategory;
        target.Amount = source.Amount;
        target.Currency = source.Currency;
        target.Member = source.Member;
        target.Account = source.Account;
        target.Note = source.Note;
        target.Type = source.Type;
        target.UpdatedAt = source.UpdatedAt;
    }
}
