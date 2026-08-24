using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces;

public interface IWorkloadService
{
    Task<WorkloadSummaryDto> GetWorkloadAsync(
        Guid requesterId,
        DateTime? from,
        DateTime? to,
        Guid? unitId,
        CancellationToken cancellationToken = default);

    Task<UserWorkloadDto> GetUserWorkloadAsync(
        Guid requesterId,
        Guid userId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<UserCapacityDto> UpdateCapacityAsync(
        Guid requesterId,
        Guid userId,
        UpdateUserCapacityDto dto,
        CancellationToken cancellationToken = default);

    Task<AssignmentPreviewDto> PreviewAssignmentAsync(
        Guid managerId,
        AssignmentPreviewRequestDto dto,
        CancellationToken cancellationToken = default);

    Task<AssignmentPreviewDto> PreviewResolvedAssignmentAsync(
        Guid managerId,
        IReadOnlyCollection<Guid> userIds,
        decimal plannedEffortHours,
        DateTime? startDate,
        DateTime? dueDate,
        CancellationToken cancellationToken = default);
}
