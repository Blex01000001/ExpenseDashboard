using ExpenseDashboard.Domain.Rules;
using ExpenseDashboard.Domain.Transactions;
using ExpenseDashboard.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseDashboard.Web.Rules;

/// <summary>規則加上目前資料中符合的支出筆數與金額（全部年份、全部貨幣），給設定頁預覽用。</summary>
public sealed record KeywordRuleSummary(KeywordRule Rule, int MatchCount, decimal MatchTotal);

/// <summary>儲存結果；<see cref="Errors"/> 為空表示成功。</summary>
public sealed record SaveResult(IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

public class KeywordRuleService(IDbContextFactory<ExpenseDbContext> dbFactory)
{
    public async Task<IReadOnlyList<KeywordRuleSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rules = await db.KeywordRules.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken);

        // 關鍵字比對（不分大小寫的包含）無法完整轉成 SQL，只撈有備註的支出在記憶體比對。
        var notes = await db.Transactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Expense && t.Note != null)
            .Select(t => new { t.Note, t.Amount })
            .ToListAsync(cancellationToken);

        return rules
            .Select(rule =>
            {
                var matched = notes.Where(n => rule.Matches(n.Note)).ToList();
                return new KeywordRuleSummary(rule, matched.Count, matched.Sum(n => n.Amount));
            })
            .ToList();
    }

    /// <param name="id">null 表示新增。</param>
    public async Task<SaveResult> SaveAsync(int? id, string? name, string? rawKeywords, CancellationToken cancellationToken = default)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var keywords = KeywordRule.ParseKeywords(rawKeywords);
        var errors = KeywordRule.Validate(trimmedName, keywords).ToList();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (errors.Count == 0 && await db.KeywordRules.AnyAsync(r => r.Name == trimmedName && r.Id != id, cancellationToken))
        {
            errors.Add($"已經有名為「{trimmedName}」的規則。");
        }

        KeywordRule? rule = null;
        if (errors.Count == 0 && id is not null)
        {
            rule = await db.KeywordRules.FindAsync([id.Value], cancellationToken);
            if (rule is null)
            {
                errors.Add("找不到這條規則，可能已被刪除。");
            }
        }

        if (errors.Count > 0)
        {
            return new SaveResult(errors);
        }

        rule ??= db.KeywordRules.Add(new KeywordRule()).Entity;
        rule.Name = trimmedName;
        rule.Keywords = keywords;
        await db.SaveChangesAsync(cancellationToken);
        return new SaveResult([]);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.KeywordRules.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}
