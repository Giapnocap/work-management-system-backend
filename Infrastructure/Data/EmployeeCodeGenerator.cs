using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.Infrastructure.Data
{
    public sealed class EmployeeCodeGenerator : IEmployeeCodeGenerator
    {
        public const string SequenceName = "EmployeeCodeSequence";

        private readonly AppDbContext _context;

        public EmployeeCodeGenerator(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
        {
            var nextValue = _context.Database.IsSqlServer()
                ? await GetNextSqlServerValueAsync(cancellationToken)
                : await GetNextNonRelationalValueAsync(cancellationToken);

            return $"EMP{nextValue:D4}";
        }

        private async Task<long> GetNextSqlServerValueAsync(CancellationToken cancellationToken)
        {
            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
                await connection.OpenAsync(cancellationToken);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"SELECT CAST(NEXT VALUE FOR [dbo].[{SequenceName}] AS bigint)";

                var currentTransaction = _context.Database.CurrentTransaction;
                if (currentTransaction != null)
                    command.Transaction = currentTransaction.GetDbTransaction();

                var value = await command.ExecuteScalarAsync(cancellationToken);
                return value == null || value == DBNull.Value
                    ? throw new InvalidOperationException("Khong the sinh ma nhan vien.")
                    : Convert.ToInt64(value);
            }
            finally
            {
                if (shouldCloseConnection)
                    await connection.CloseAsync();
            }
        }

        private async Task<long> GetNextNonRelationalValueAsync(CancellationToken cancellationToken)
        {
            var employeeCodes = await _context.Users
                .IgnoreQueryFilters()
                .Where(user => user.EmployeeCode.StartsWith("EMP"))
                .Select(user => user.EmployeeCode)
                .ToListAsync(cancellationToken);

            return employeeCodes
                .Select(code => long.TryParse(code.AsSpan(3), out var value) ? value : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;
        }
    }
}
