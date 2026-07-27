namespace WorkManagementSystem.Application.Interfaces
{
    public interface IEmployeeCodeGenerator
    {
        Task<string> GenerateAsync(CancellationToken cancellationToken = default);
    }
}
