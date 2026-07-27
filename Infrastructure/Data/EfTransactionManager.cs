using System.Data;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.Infrastructure.Data
{
    public sealed class EfTransactionManager : ITransactionManager
    {
        private readonly AppDbContext _context;

        public EfTransactionManager(AppDbContext context)
        {
            _context = context;
        }

        public Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(async token =>
            {
                await operation(token);
                return true;
            }, cancellationToken);
        }

        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
            => await ExecuteCoreAsync(operation, null, cancellationToken);

        public async Task<T> ExecuteSerializableAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
            => await ExecuteCoreAsync(operation, IsolationLevel.Serializable, cancellationToken);

        private async Task<T> ExecuteCoreAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            IsolationLevel? isolationLevel,
            CancellationToken cancellationToken)
        {
            if (!_context.Database.IsRelational())
                return await operation(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = isolationLevel.HasValue
                    ? await _context.Database.BeginTransactionAsync(isolationLevel.Value, cancellationToken)
                    : await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var result = await operation(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });
        }
    }
}
