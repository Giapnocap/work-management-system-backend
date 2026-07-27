using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IProjectService
    {
        Task<List<ProjectDto>> GetProjects(Guid userId, CancellationToken cancellationToken = default);
        Task<ProjectDto> CreateProject(
            CreateProjectDto dto,
            Guid userId,
            CancellationToken cancellationToken = default);
        Task<ProjectDto> UpdateProject(
            Guid id,
            CreateProjectDto dto,
            Guid userId,
            CancellationToken cancellationToken = default);
        Task ArchiveProject(Guid id, Guid userId, CancellationToken cancellationToken = default);
    }
}
