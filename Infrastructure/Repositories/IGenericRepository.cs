namespace WorkManagementSystem.Infrastructure.Repositories
{
    public interface IGenericRepository<T>
    {
        IQueryable<T> Query();
        IQueryable<T> QueryReadOnly();
        Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task AddAsync(T entity, CancellationToken cancellationToken = default);
        void Update(T entity);
        void Delete(T entity);
        Task SaveAsync(CancellationToken cancellationToken = default);
    }
}
