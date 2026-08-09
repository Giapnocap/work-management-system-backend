using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class OptimisticConcurrencyTests
{
    [Fact]
    public async Task UpdateUnit_WithStaleRowVersion_DoesNotOverwriteCurrentData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateUnitTable(connection);

        var unitId = Guid.NewGuid();
        var staleRowVersion = new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 };
        var currentRowVersion = new byte[] { 2, 2, 2, 2, 2, 2, 2, 2 };
        await InsertUnit(connection, unitId, currentRowVersion);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        var service = CreateUnitService(context);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.Update(
            unitId,
            new UpdateUnitDto
            {
                Name = "Stale overwrite",
                RowVersion = staleRowVersion
            }));

        context.ChangeTracker.Clear();
        var persisted = await context.Units.AsNoTracking().SingleAsync(unit => unit.Id == unitId);
        Assert.Equal("Engineering", persisted.Name);
        Assert.Equal(currentRowVersion, persisted.RowVersion);
    }

    private static UnitService CreateUnitService(AppDbContext context)
    {
        return new UnitService(
            TestFactory.Repo<WorkManagementSystem.Domain.Entities.Unit>(context),
            TestFactory.Repo<WorkManagementSystem.Domain.Entities.UserUnit>(context),
            TestFactory.Repo<WorkManagementSystem.Domain.Entities.User>(context),
            TestFactory.CreateStaffMovementService(context),
            TestFactory.CreateMapper(),
            new EfTransactionManager(context),
            new NoOpAuditService(),
            context);
    }

    private static async Task CreateUnitTable(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Units (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                IsDeleted INTEGER NOT NULL,
                RowVersion BLOB NOT NULL
            )
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertUnit(
        SqliteConnection connection,
        Guid unitId,
        byte[] rowVersion)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Units (Id, Name, IsDeleted, RowVersion)
            VALUES ($id, $name, 0, $rowVersion)
            """;
        command.Parameters.AddWithValue("$id", unitId.ToString().ToUpperInvariant());
        command.Parameters.AddWithValue("$name", "Engineering");
        command.Parameters.AddWithValue("$rowVersion", rowVersion);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task RecordAsync(
            string entityType,
            Guid entityId,
            string action,
            Guid? actorUserId,
            object? details = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<AuditLogPageDto> GetAsync(
            string? entityType,
            Guid? entityId,
            string? action,
            Guid? actorUserId,
            DateTime? from,
            DateTime? to,
            int page,
            int size,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AuditLogPageDto());
    }
}
