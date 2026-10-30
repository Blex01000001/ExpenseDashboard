using ExpenseDashboard.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseDashboard.Tests.Data;

public class ExpenseDbContextTests
{
    [Fact]
    public void Migrations_cover_the_current_model()
    {
        using var database = new SqliteTestDatabase();
        using var db = database.CreateDbContext();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Transaction_round_trips_through_sqlite()
    {
        using var database = new SqliteTestDatabase();
        var transaction = NewTransaction(amount: 550m);

        await using (var db = database.CreateDbContext())
        {
            db.Transactions.Add(transaction);
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext())
        {
            var saved = await db.Transactions.SingleAsync();
            Assert.Equal(transaction.ExternalId, saved.ExternalId);
            Assert.Equal(new DateOnly(2025, 1, 19), saved.Date);
            Assert.Equal(550m, saved.Amount);
            Assert.Equal(TransactionType.Expense, saved.Type);
            Assert.Equal(new DateTime(2025, 1, 19, 19, 55, 9), saved.UpdatedAt);
        }
    }

    [Fact]
    public async Task ExternalId_must_be_unique()
    {
        using var database = new SqliteTestDatabase();
        var externalId = Guid.NewGuid();

        await using var db = database.CreateDbContext();
        db.Transactions.Add(NewTransaction(externalId: externalId));
        db.Transactions.Add(NewTransaction(externalId: externalId));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Decimal_amounts_can_be_summed_in_sqlite()
    {
        using var database = new SqliteTestDatabase();

        await using var db = database.CreateDbContext();
        db.Transactions.AddRange(NewTransaction(amount: 25m), NewTransaction(amount: 30.5m));
        await db.SaveChangesAsync();

        Assert.Equal(55.5m, await db.Transactions.SumAsync(t => t.Amount));
    }

    private static Transaction NewTransaction(Guid? externalId = null, decimal amount = 100m) => new()
    {
        ExternalId = externalId ?? Guid.NewGuid(),
        Date = new DateOnly(2025, 1, 19),
        Category = "晚餐",
        MajorCategory = "Diet",
        Amount = amount,
        Currency = "TWD",
        Member = "自己",
        Account = "現金",
        Type = TransactionType.Expense,
        UpdatedAt = new DateTime(2025, 1, 19, 19, 55, 9),
    };
}
