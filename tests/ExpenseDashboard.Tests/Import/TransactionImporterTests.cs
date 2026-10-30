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

        var summary = await importer.ImportAsync([NewTransaction(), NewTransaction()]);

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

        var summary = await importer.ImportAsync([NewTransaction(externalId)]);

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
        var summary = await importer.ImportAsync([newer]);

        Assert.Equal(new ImportSummary(Added: 0, Updated: 1, Unchanged: 0), summary);
        await using var db = database.CreateDbContext();
        var saved = await db.Transactions.SingleAsync();
        Assert.Equal(250m, saved.Amount);
        Assert.Equal("晚餐", saved.Category);
        Assert.Equal("改過", saved.Note);
        Assert.Equal(BaseTime.AddMinutes(5), saved.UpdatedAt);
    }

    [Fact]
    public async Task Older_version_does_not_overwrite_existing_transaction()
    {
        using var database = new SqliteTestDatabase();
        var importer = new TransactionImporter(database);
        var externalId = Guid.NewGuid();
        await importer.ImportAsync([NewTransaction(externalId, amount: 100m)]);

        var summary = await importer.ImportAsync([NewTransaction(externalId, amount: 1m, updatedAt: BaseTime.AddDays(-1))]);

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

        var summary = await importer.ImportAsync(
        [
            NewTransaction(externalId, amount: 300m, updatedAt: BaseTime.AddHours(1)),
            NewTransaction(externalId, amount: 100m),
        ]);

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

        var summary = await importer.ImportAsync(
            ids.Select(id => NewTransaction(id, updatedAt: BaseTime.AddMinutes(1))).Append(NewTransaction()).ToList());

        Assert.Equal(new ImportSummary(Added: 1, Updated: 1200, Unchanged: 0), summary);
    }

    private static Transaction NewTransaction(Guid? externalId = null, decimal amount = 100m, DateTime? updatedAt = null) => new()
    {
        ExternalId = externalId ?? Guid.NewGuid(),
        Date = new DateOnly(2025, 1, 4),
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
