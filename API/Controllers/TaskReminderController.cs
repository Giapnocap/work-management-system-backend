using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers;

[Authorize]
[ApiController]
[Route("api/tasks/{taskId:guid}/reminders")]
[Tags("Task")]
public sealed class TaskReminderController : ControllerBase
{
    private readonly IDeadlineReminderService _service;
    private readonly ICurrentUserService _currentUser;

    public TaskReminderController(
        IDeadlineReminderService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<ScheduledNotificationDto>>> Get(Guid taskId)
    {
        return Ok(await _service.GetTaskRemindersAsync(
            taskId,
            _currentUser.GetRequiredUserId(),
            HttpContext.RequestAborted));
    }
}
