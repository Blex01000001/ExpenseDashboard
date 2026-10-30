using ExpenseDashboard.Web.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExpenseDashboard.Tests.Data;

/// <summary>
/// 以 in-memory SQLite 建立的測試資料庫；連線保持開啟期間資料才存在。
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<ExpenseDbContext> options;

    public SqliteTestDatabase()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<ExpenseDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = CreateContext();
        db.Database.Migrate();
    }

    public ExpenseDbContext CreateContext() => new(options);

    public void Dispose() => connection.Dispose();
}
