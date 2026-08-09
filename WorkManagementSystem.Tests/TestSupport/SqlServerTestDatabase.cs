using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Tests.TestSupport;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string ConnectionStringEnvironmentVariable = "WMS_TEST_SQLSERVER_CONNECTION";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)))
        {
            Skip = $"Set {ConnectionStringEnvironmentVariable} to run SQL Server integration tests.";
        }
    }
}

public sealed class SqlServerTestDatabase : IAsyncLifetime
{
    private string? _adminConnectionString;
    private string? _databaseName;
    private bool _databaseCreated;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable(
            SqlServerFactAttribute.ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException(
                $"{SqlServerFactAttribute.ConnectionStringEnvironmentVariable} is required.");
        }

        var adminBuilder = new SqlConnectionStringBuilder(configuredConnectionString)
        {
            InitialCatalog = "master"
        };
        _adminConnectionString = adminBuilder.ConnectionString;
        _databaseName = $"WmsTests_{Guid.NewGuid():N}";

        await using (var connection = new SqlConnection(_adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
            _databaseCreated = true;
        }

        var databaseBuilder = new SqlConnectionStringBuilder(adminBuilder.ConnectionString)
        {
            InitialCatalog = _databaseName
        };
        ConnectionString = databaseBuilder.ConnectionString;

        try
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }
        catch
        {
            await DropDatabaseAsync();
            throw;
        }
    }

    public AppDbContext CreateContext()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("The SQL Server test database is not initialized.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options;

        return new AppDbContext(options);
    }

    public Task DisposeAsync() => DropDatabaseAsync();

    private async Task DropDatabaseAsync()
    {
        if (!_databaseCreated ||
            string.IsNullOrWhiteSpace(_adminConnectionString) ||
            string.IsNullOrWhiteSpace(_databaseName))
        {
            return;
        }

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

        _databaseCreated = false;
        _databaseName = null;
        ConnectionString = string.Empty;
    }
}
