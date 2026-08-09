using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Tests;

public class EfTransactionManagerTests
{
    [Fact]
    public async Task ExecuteSerializableAsync_WhenOperationFails_RollsBackDatabaseChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await CreateProbeTable(context);
        var transactionManager = new EfTransactionManager(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transactionManager.ExecuteSerializableAsync<bool>(async cancellationToken =>
            {
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO TransactionProbe (Value) VALUES ('rollback')",
                    cancellationToken);
                throw new InvalidOperationException("Force rollback");
            }));

        Assert.Equal(0L, await CountRows(connection));
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_CommitsDatabaseChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await CreateProbeTable(context);
        var transactionManager = new EfTransactionManager(context);

        await transactionManager.ExecuteAsync(async cancellationToken =>
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO TransactionProbe (Value) VALUES ('commit')",
                cancellationToken);
        });

        Assert.Equal(1L, await CountRows(connection));
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private static Task CreateProbeTable(AppDbContext context)
        => context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE TransactionProbe (Id INTEGER PRIMARY KEY, Value TEXT NOT NULL)");

    private static async Task<long> CountRows(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TransactionProbe";
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }
}
