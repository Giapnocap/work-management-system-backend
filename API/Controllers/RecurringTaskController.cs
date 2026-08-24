using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers;

[Authorize(Roles = SystemRoles.Manager)]
[ApiController]
[Route("api/recurring-tasks")]
[Tags("RecurringTask")]
public sealed class RecurringTaskController : ControllerBase
{
    private readonly IRecurringTaskService _service;
    private readonly ICurrentUserService _currentUser;

    public RecurringTaskController(
        IRecurringTaskService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<RecurringTaskTemplateDto>>> GetAll()
    {
        return Ok(await _service.GetAllAsync(
            _currentUser.GetRequiredUserId(),
            HttpContext.RequestAborted));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecurringTaskTemplateDto>> GetById(Guid id)
    {
        return Ok(await _service.GetByIdAsync(
            id,
            _currentUser.GetRequiredUserId(),
            HttpContext.RequestAborted));
    }

    [HttpPost]
    public async Task<ActionResult<RecurringTaskTemplateDto>> Create(CreateRecurringTaskDto dto)
    {
        var result = await _service.CreateAsync(
            dto,
            _currentUser.GetRequiredUserId(),
            HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecurringTaskTemplateDto>> Update(
        Guid id,
        UpdateRecurringTaskDto dto)
    {
        return Ok(await _service.UpdateAsync(
            id,
            dto,
            _currentUser.GetRequiredUserId(),
            HttpContext.RequestAborted));
    }

    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<RecurringTaskTemplateDto>> Pause(Guid id)
    {
        return Ok(await _service.PauseAsync(
            id,
            _currentUser.GetRequiredUserId(),
            HttpContext.RequestAborted));
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<RecurringTaskTemplateDto>> Resume(Guid id)
    {
        return Ok(await _service.ResumeAsync(
            id,
            _currentUser.GetRequiredUserId(),
            HttpContext.RequestAborted));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(
            id,
            _currentUser.GetRequiredUserId(),
            HttpContext.RequestAborted);
        return NoContent();
    }
}
