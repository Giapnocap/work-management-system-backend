using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IReviewService
    {
        Task<ReviewDto> Review(ReviewDto dto, Guid reviewerId, CancellationToken cancellationToken = default);
    }
}
