using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

[Trait("Category", "SqlServer")]
public sealed class SqlServerRelationalTests : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;

    public SqlServerRelationalTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    [SqlServerFact]
    public async Task Migrations_FromEmptyDatabase_ApplyCompleteSchema()
    {
        await using var context = _database.CreateContext();

        var expectedMigrations = context.Database.GetMigrations().ToArray();
        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();

        Assert.NotEmpty(expectedMigrations);
        Assert.Equal(expectedMigrations, appliedMigrations);
        Assert.True(await context.Database.CanConnectAsync());
    }

    [SqlServerFact]
    public async Task UniqueConstraint_RejectsDuplicateUsername()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var context = _database.CreateContext();
        context.Users.AddRange(
            CreateUser($"duplicate-{suffix}", $"EMP-{suffix}-1"),
            CreateUser($"duplicate-{suffix}", $"EMP-{suffix}-2"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task ForeignKeyConstraint_RejectsMembershipWithoutUserAndUnit()
    {
        await using var context = _database.CreateContext();
        context.UserUnits.Add(new UserUnit
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UnitId = Guid.NewGuid()
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task CheckConstraint_RejectsInvalidKpiPeriodDateRange()
    {
        var today = DateTime.UtcNow.Date;
        await using var context = _database.CreateContext();
        context.KpiPeriods.Add(new KpiPeriod
        {
            Id = Guid.NewGuid(),
            Name = $"Invalid period {Guid.NewGuid():N}",
            StartDate = today.AddDays(1),
            EndDate = today,
            CreatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Transaction_WhenOperationFails_RollsBackPersistedChanges()
    {
        var unitId = Guid.NewGuid();
        await using (var context = _database.CreateContext())
        {
            var transactionManager = new EfTransactionManager(context);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                transactionManager.ExecuteAsync(async cancellationToken =>
                {
                    context.Units.Add(new Unit
                    {
                        Id = unitId,
                        Name = $"Rollback {Guid.NewGuid():N}"
                    });
                    await context.SaveChangesAsync(cancellationToken);
                    throw new InvalidOperationException("Force rollback");
                }));
        }

        await using var verificationContext = _database.CreateContext();
        Assert.False(await verificationContext.Units
            .IgnoreQueryFilters()
            .AnyAsync(unit => unit.Id == unitId));
    }

    [SqlServerFact]
    public async Task RowVersion_WhenConcurrentUpdateUsesStaleToken_PreventsLostUpdate()
    {
        var unitId = Guid.NewGuid();
        await using (var setupContext = _database.CreateContext())
        {
            setupContext.Units.Add(new Unit
            {
                Id = unitId,
                Name = $"Concurrency {Guid.NewGuid():N}"
            });
            await setupContext.SaveChangesAsync();
        }

        await using var firstContext = _database.CreateContext();
        await using var staleContext = _database.CreateContext();
        var firstCopy = await firstContext.Units.SingleAsync(unit => unit.Id == unitId);
        var staleCopy = await staleContext.Units.SingleAsync(unit => unit.Id == unitId);

        firstCopy.Name = $"First update {Guid.NewGuid():N}";
        await firstContext.SaveChangesAsync();

        staleCopy.Name = $"Stale update {Guid.NewGuid():N}";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());

        await using var verificationContext = _database.CreateContext();
        var persistedName = await verificationContext.Units
            .Where(unit => unit.Id == unitId)
            .Select(unit => unit.Name)
            .SingleAsync();
        Assert.Equal(firstCopy.Name, persistedName);
    }

    private static User CreateUser(string username, string employeeCode)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = "SQL integration user",
            EmployeeCode = employeeCode,
            PasswordHash = "not-used-by-this-test",
            Role = SystemRoles.User,
            JoinedUnitAt = DateTime.UtcNow,
            IsApproved = true
        };
    }
}
