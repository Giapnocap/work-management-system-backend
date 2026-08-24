using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers;

[Authorize(Roles = SystemRoles.AdminOrManager)]
[ApiController]
[Route("api/users")]
public sealed class UserCapacityController : ControllerBase
{
    private readonly IWorkloadService _workloadService;
    private readonly ICurrentUserService _currentUser;

    public UserCapacityController(
        IWorkloadService workloadService,
        ICurrentUserService currentUser)
    {
        _workloadService = workloadService;
        _currentUser = currentUser;
    }

    /// <summary>Cập nhật capacity tuần của nhân viên từ ngày áp dụng.</summary>
    [HttpPut("{userId:guid}/capacity")]
    public async Task<ActionResult<UserCapacityDto>> Update(
        Guid userId,
        UpdateUserCapacityDto dto)
    {
        var requesterId = _currentUser.GetRequiredUserId();
        return Ok(await _workloadService.UpdateCapacityAsync(
            requesterId,
            userId,
            dto,
            HttpContext.RequestAborted));
    }
}
