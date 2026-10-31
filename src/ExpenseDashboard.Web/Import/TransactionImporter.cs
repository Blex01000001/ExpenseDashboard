using ExpenseDashboard.Domain.Transactions;
using ExpenseDashboard.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseDashboard.Web.Import;

public sealed record ImportSummary(int Added, int Updated, int Unchanged);

/// <summary>
/// 以 <see cref="Transaction.ExternalId"/> 做 upsert：新的 UUID 新增；
/// 已存在且 <see cref="Transaction.UpdatedAt"/> 較新、或時間相同但內容不同的覆寫；
/// 時間較舊的不動（避免舊的匯出檔把新資料蓋回去）。
/// 匯出檔裡沒有出現的既有資料不會自動刪除，要另外經過 <see cref="FindMissingAsync"/> 預覽再 <see cref="DeleteAsync"/>。
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
            else if (transaction.UpdatedAt > current.UpdatedAt
                     || (transaction.UpdatedAt == current.UpdatedAt && !transaction.HasSameContent(current)))
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

    /// <summary>
    /// 匯出檔日期範圍（<paramref name="exported"/> 中最早到最晚的日期，含兩端）內，資料庫有、但匯出檔沒有的紀錄，
    /// 也就是在 App 裡被刪除的紀錄。範圍外的紀錄一律不列入，避免只匯出部分期間時誤刪。依日期新到舊。
    /// </summary>
    public async Task<IReadOnlyList<Transaction>> FindMissingAsync(
        IReadOnlyCollection<Transaction> exported, CancellationToken cancellationToken = default)
    {
        if (exported.Count == 0)
        {
            return [];
        }

        var from = exported.Min(t => t.Date);
        var to = exported.Max(t => t.Date);
        var exportedIds = exported.Select(t => t.ExternalId).ToHashSet();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var inRange = await db.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= from && t.Date <= to)
            .ToListAsync(cancellationToken);

        return inRange
            .Where(t => !exportedIds.Contains(t.ExternalId))
            .OrderByDescending(t => t.Date)
            .ThenBy(t => t.Id)
            .ToList();
    }

    /// <summary>刪除指定 Id 的紀錄（只刪預覽時列出的那些），回傳實際刪除筆數。</summary>
    public async Task<int> DeleteAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var deleted = 0;
        foreach (var chunk in ids.Chunk(LookupBatchSize))
        {
            deleted += await db.Transactions.Where(t => chunk.Contains(t.Id)).ExecuteDeleteAsync(cancellationToken);
        }
        return deleted;
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
        target.Tags = source.Tags;
        target.Type = source.Type;
        target.UpdatedAt = source.UpdatedAt;
    }
}
