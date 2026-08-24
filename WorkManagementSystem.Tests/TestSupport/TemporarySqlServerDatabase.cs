using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Tests.TestSupport;

internal sealed class TemporarySqlServerDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _databaseName;
    private bool _created;

    private TemporarySqlServerDatabase(
        string adminConnectionString,
        string databaseName,
        string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
        _created = true;
    }

    public string ConnectionString { get; }

    public static async Task<TemporarySqlServerDatabase> CreateAsync(
        string serverConnectionString,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        var adminBuilder = new SqlConnectionStringBuilder(serverConnectionString)
        {
            InitialCatalog = "master"
        };
        var databaseName = $"{prefix}_{Guid.NewGuid():N}";

        await using (var connection = new SqlConnection(adminBuilder.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var databaseBuilder = new SqlConnectionStringBuilder(adminBuilder.ConnectionString)
        {
            InitialCatalog = databaseName
        };
        return new TemporarySqlServerDatabase(
            adminBuilder.ConnectionString,
            databaseName,
            databaseBuilder.ConnectionString);
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options;
        return new AppDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_created)
            return;

        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{_databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_databaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
        _created = false;
    }
}
