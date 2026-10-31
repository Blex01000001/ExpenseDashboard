using ExpenseDashboard.Domain.Transactions;
using ExpenseDashboard.Tests.Data;
using ExpenseDashboard.Web.Import;
using Microsoft.EntityFrameworkCore;

namespace ExpenseDashboard.Tests.Import;

public class TransactionImporterTests
{
    private static readonly DateTime BaseTime = new(2025, 1, 4, 12, 0, 0);

    [Fact]
    public async Task New_transactions_are_added()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);

        var summary = (await importer.ImportAsync([NewTransaction(), NewTransaction()])).Summary;

        Assert.Equal(new ImportSummary(Added: 2, Updated: 0, Unchanged: 0), summary);
        await using var db = database.CreateDbContext();
        Assert.Equal(2, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Reimporting_the_same_data_changes_nothing()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        var externalId = Guid.NewGuid();
        await importer.ImportAsync([NewTransaction(externalId)]);

        var summary = (await importer.ImportAsync([NewTransaction(externalId)])).Summary;

        Assert.Equal(new ImportSummary(Added: 0, Updated: 0, Unchanged: 1), summary);
        await using var db = database.CreateDbContext();
        Assert.Equal(1, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Newer_version_overwrites_existing_transaction()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        var externalId = Guid.NewGuid();
        await importer.ImportAsync([NewTransaction(externalId, amount: 100m)]);

        var newer = NewTransaction(externalId, amount: 250m, updatedAt: BaseTime.AddMinutes(5));
        newer.Category = "晚餐";
        newer.Note = "改過";
        newer.Tags = "新標籤";
        var summary = (await importer.ImportAsync([newer])).Summary;

        Assert.Equal(new ImportSummary(Added: 0, Updated: 1, Unchanged: 0), summary);
        await using var db = database.CreateDbContext();
        var saved = await db.Transactions.SingleAsync();
        Assert.Equal(250m, saved.Amount);
        Assert.Equal("晚餐", saved.Category);
        Assert.Equal("改過", saved.Note);
        Assert.Equal("新標籤", saved.Tags);
        Assert.Equal(BaseTime.AddMinutes(5), saved.UpdatedAt);
    }

    [Fact]
    public async Task Older_version_does_not_overwrite_existing_transaction()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        var externalId = Guid.NewGuid();
        await importer.ImportAsync([NewTransaction(externalId, amount: 100m)]);

        var summary = (await importer.ImportAsync([NewTransaction(externalId, amount: 1m, updatedAt: BaseTime.AddDays(-1))])).Summary;

        Assert.Equal(new ImportSummary(Added: 0, Updated: 0, Unchanged: 1), summary);
        await using var db = database.CreateDbContext();
        Assert.Equal(100m, (await db.Transactions.SingleAsync()).Amount);
    }

    [Fact]
    public async Task Duplicate_uuids_within_one_import_keep_the_latest_version()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        var externalId = Guid.NewGuid();

        var summary = (await importer.ImportAsync(
        [
            NewTransaction(externalId, amount: 300m, updatedAt: BaseTime.AddHours(1)),
            NewTransaction(externalId, amount: 100m),
        ])).Summary;

        Assert.Equal(new ImportSummary(Added: 1, Updated: 0, Unchanged: 0), summary);
        await using var db = database.CreateDbContext();
        Assert.Equal(300m, (await db.Transactions.SingleAsync()).Amount);
    }

    [Fact]
    public async Task Existing_transactions_missing_from_the_import_are_kept()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        await importer.ImportAsync([NewTransaction()]);

        await importer.ImportAsync([NewTransaction()]);

        await using var db = database.CreateDbContext();
        Assert.Equal(2, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Large_import_spanning_several_lookup_batches_is_upserted()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        var ids = Enumerable.Range(0, 1200).Select(_ => Guid.NewGuid()).ToList();
        await importer.ImportAsync(ids.Select(id => NewTransaction(id)).ToList());

        var summary = (await importer.ImportAsync(
            ids.Select(id => NewTransaction(id, updatedAt: BaseTime.AddMinutes(1))).Append(NewTransaction()).ToList())).Summary;

        Assert.Equal(new ImportSummary(Added: 1, Updated: 1200, Unchanged: 0), summary);
    }

    [Fact]
    public async Task Same_timestamp_with_different_content_overwrites()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        var externalId = Guid.NewGuid();
        await importer.ImportAsync([NewTransaction(externalId)]);

        var renamed = NewTransaction(externalId);
        renamed.Category = "便當";
        var summary = (await importer.ImportAsync([renamed])).Summary;

        Assert.Equal(new ImportSummary(Added: 0, Updated: 1, Unchanged: 0), summary);
        await using var db = database.CreateDbContext();
        Assert.Equal("便當", (await db.Transactions.SingleAsync()).Category);
    }

    [Fact]
    public async Task Import_result_lists_changed_fields_of_overwritten_records()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        var externalId = Guid.NewGuid();
        var unchangedId = Guid.NewGuid();
        await importer.ImportAsync([NewTransaction(externalId, amount: 200m), NewTransaction(unchangedId)]);

        var edited = NewTransaction(externalId, amount: 250m, updatedAt: BaseTime.AddMinutes(1));
        edited.Note = "加點飲料";
        var touched = NewTransaction(unchangedId, updatedAt: BaseTime.AddMinutes(1));
        var result = await importer.ImportAsync([edited, touched, NewTransaction()]);

        Assert.Equal(new ImportSummary(Added: 1, Updated: 2, Unchanged: 0), result.Summary);
        var amountChange = Assert.Single(result.Changes, c => c.Amount == 250m);
        Assert.Equal(
            [new FieldChange("金額", "200", "250"), new FieldChange("備註", "（空）", "加點飲料")],
            amountChange.Changes);
        // 只有「上次更新」變了：列出但沒有欄位變動。
        Assert.Single(result.Changes, c => c.Changes.Count == 0);
    }

    [Fact]
    public void ChangesFrom_formats_dates_types_and_empty_values()
    {
        var before = NewTransaction();
        var after = NewTransaction(before.ExternalId, date: new DateOnly(2025, 2, 1));
        after.MajorCategory = null;
        after.Type = TransactionType.Income;

        Assert.Equal(
            [
                new FieldChange("日期", "2025/01/04", "2025/02/01"),
                new FieldChange("大類別", "Diet", "（空）"),
                new FieldChange("收支區分", "支", "收"),
            ],
            after.ChangesFrom(before));
    }

    [Fact]
    public async Task Older_timestamp_with_different_content_is_ignored()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        var externalId = Guid.NewGuid();
        await importer.ImportAsync([NewTransaction(externalId)]);

        var stale = NewTransaction(externalId, updatedAt: BaseTime.AddSeconds(-1));
        stale.Category = "舊名稱";
        var summary = (await importer.ImportAsync([stale])).Summary;

        Assert.Equal(new ImportSummary(Added: 0, Updated: 0, Unchanged: 1), summary);
        await using var db = database.CreateDbContext();
        Assert.Equal("午餐", (await db.Transactions.SingleAsync()).Category);
    }

    [Theory]
    [InlineData(nameof(Transaction.Note))]
    [InlineData(nameof(Transaction.Tags))]
    [InlineData(nameof(Transaction.MajorCategory))]
    [InlineData(nameof(Transaction.Account))]
    public void HasSameContent_detects_each_field(string field)
    {
        var a = NewTransaction();
        var b = NewTransaction(a.ExternalId);
        Assert.True(a.HasSameContent(b));

        typeof(Transaction).GetProperty(field)!.SetValue(b, "改過");

        Assert.False(a.HasSameContent(b));
    }

    [Fact]
    public async Task FindMissing_lists_only_records_inside_the_export_date_range()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        var kept = NewTransaction(date: new DateOnly(2025, 1, 10));
        var deletedInApp = NewTransaction(date: new DateOnly(2025, 1, 15));
        var onFirstDay = NewTransaction(date: new DateOnly(2025, 1, 5));
        var beforeRange = NewTransaction(date: new DateOnly(2025, 1, 4));
        var afterRange = NewTransaction(date: new DateOnly(2025, 2, 1));
        var exportedFirst = NewTransaction(date: new DateOnly(2025, 1, 5));
        var exportedLast = NewTransaction(date: new DateOnly(2025, 1, 20));
        await importer.ImportAsync([kept, deletedInApp, onFirstDay, beforeRange, afterRange, exportedFirst, exportedLast]);

        // 新匯出檔：1/5–1/20，缺了 deletedInApp 與 onFirstDay。
        var missing = await importer.FindMissingAsync([Copy(kept), Copy(exportedFirst), Copy(exportedLast)]);

        Assert.Equal([deletedInApp.ExternalId, onFirstDay.ExternalId], missing.Select(t => t.ExternalId));
    }

    [Fact]
    public async Task FindMissing_of_an_empty_export_is_empty()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        await importer.ImportAsync([NewTransaction()]);

        Assert.Empty(await importer.FindMissingAsync([]));
    }

    [Fact]
    public async Task Delete_removes_only_the_given_records()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        var keep = NewTransaction();
        var remove = NewTransaction();
        await importer.ImportAsync([keep, remove]);
        var missing = await importer.FindMissingAsync([Copy(keep)]);

        var deleted = await importer.DeleteAsync(missing.Select(t => t.Id).ToList());

        Assert.Equal(1, deleted);
        await using var db = database.CreateDbContext();
        Assert.Equal(keep.ExternalId, (await db.Transactions.SingleAsync()).ExternalId);
    }

    /// <summary>模擬下一次匯出：同一筆紀錄的新物件（尚未存進資料庫）。</summary>
    private static Transaction Copy(Transaction t) =>
        NewTransaction(t.ExternalId, t.Amount, t.UpdatedAt, t.Date);

    private static Transaction NewTransaction(
        Guid? externalId = null, decimal amount = 100m, DateTime? updatedAt = null, DateOnly? date = null) => new()
    {
        ExternalId = externalId ?? Guid.NewGuid(),
        Date = date ?? new DateOnly(2025, 1, 4),
        Category = "午餐",
        MajorCategory = "Diet",
        Amount = amount,
        Currency = "TWD",
        Member = "自己",
        Account = "現金",
        Type = TransactionType.Expense,
        UpdatedAt = updatedAt ?? BaseTime,
    };
}
