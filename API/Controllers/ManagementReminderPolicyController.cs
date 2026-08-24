using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers;

[Authorize(Roles = SystemRoles.AdminOrManager)]
[ApiController]
[Route("api/management/reminder-policies")]
[Tags("ManagementReminder")]
public sealed class ManagementReminderPolicyController : ControllerBase
{
    private readonly IReminderPolicyService _service;
    private readonly ICurrentUserService _currentUser;

    public ManagementReminderPolicyController(
        IReminderPolicyService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<ReminderPolicyDto>>> Get()
    {
        return Ok(await _service.GetAsync(
            _currentUser.GetRequiredUserId(),
            HttpContext.RequestAborted));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReminderPolicyDto>> Upsert(
        Guid id,
        UpsertReminderPolicyDto dto)
    {
        return Ok(await _service.UpsertAsync(
            id,
            dto,
            _currentUser.GetRequiredUserId(),
            HttpContext.RequestAborted));
    }
}
