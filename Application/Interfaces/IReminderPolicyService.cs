using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces;

public interface IReminderPolicyService
{
    Task<List<ReminderPolicyDto>> GetAsync(
        Guid requesterId,
        CancellationToken cancellationToken = default);

    Task<ReminderPolicyDto> UpsertAsync(
        Guid id,
        UpsertReminderPolicyDto dto,
        Guid requesterId,
        CancellationToken cancellationToken = default);
}
