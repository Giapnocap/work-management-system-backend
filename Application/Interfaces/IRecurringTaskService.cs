using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces;

public interface IRecurringTaskService
{
    Task<List<RecurringTaskTemplateDto>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecurringTaskTemplateDto> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecurringTaskTemplateDto> CreateAsync(
        CreateRecurringTaskDto dto,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecurringTaskTemplateDto> UpdateAsync(
        Guid id,
        UpdateRecurringTaskDto dto,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecurringTaskTemplateDto> PauseAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecurringTaskTemplateDto> ResumeAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);
}
