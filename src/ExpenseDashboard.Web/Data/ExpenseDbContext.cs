using ExpenseDashboard.Domain.Rules;
using ExpenseDashboard.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseDashboard.Web.Data;

public class ExpenseDbContext(DbContextOptions<ExpenseDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<KeywordRule> KeywordRules => Set<KeywordRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasIndex(t => t.ExternalId).IsUnique();
            entity.HasIndex(t => t.Date);

            entity.Property(t => t.Category).HasMaxLength(100);
            entity.Property(t => t.MajorCategory).HasMaxLength(100);
            entity.Property(t => t.Currency).HasMaxLength(3);
            entity.Property(t => t.Member).HasMaxLength(100);
            entity.Property(t => t.Account).HasMaxLength(100);
            entity.Property(t => t.Tags).HasMaxLength(200);
            entity.Property(t => t.Type).HasConversion<string>().HasMaxLength(10);
        });

        modelBuilder.Entity<KeywordRule>(entity =>
        {
            entity.HasIndex(r => r.Name).IsUnique();
            entity.Property(r => r.Name).HasMaxLength(KeywordRule.MaxNameLength);
        });
    }
}
