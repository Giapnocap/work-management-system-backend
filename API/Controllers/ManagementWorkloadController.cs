using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers;

[Authorize(Roles = SystemRoles.AdminOrManager)]
[ApiController]
[Route("api/management/workload")]
public sealed class ManagementWorkloadController : ControllerBase
{
    private readonly IWorkloadService _workloadService;
    private readonly ICurrentUserService _currentUser;

    public ManagementWorkloadController(
        IWorkloadService workloadService,
        ICurrentUserService currentUser)
    {
        _workloadService = workloadService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<WorkloadSummaryDto>> Get(
        DateTime? from,
        DateTime? to,
        Guid? unitId)
    {
        var requesterId = _currentUser.GetRequiredUserId();
        return Ok(await _workloadService.GetWorkloadAsync(
            requesterId,
            from,
            to,
            unitId,
            HttpContext.RequestAborted));
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserWorkloadDto>> GetUser(
        Guid userId,
        DateTime? from,
        DateTime? to)
    {
        var requesterId = _currentUser.GetRequiredUserId();
        return Ok(await _workloadService.GetUserWorkloadAsync(
            requesterId,
            userId,
            from,
            to,
            HttpContext.RequestAborted));
    }
}
