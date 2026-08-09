using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize(Roles = SystemRoles.Manager)]
    [ApiController]
    [Route("api/projects")]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectService _service;
        private readonly ICurrentUserService _currentUser;

        public ProjectController(IProjectService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<ActionResult<List<ProjectDto>>> GetProjects()
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetProjects(userId, HttpContext.RequestAborted));
        }

        [HttpPost]
        [Authorize(Roles = SystemRoles.Manager)]
        public async Task<ActionResult<ProjectDto>> Create(CreateProjectDto dto)
        {
            var userId = _currentUser.GetRequiredUserId();
            var result = await _service.CreateProject(dto, userId, HttpContext.RequestAborted);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = SystemRoles.Manager)]
        public async Task<ActionResult<ProjectDto>> Update(Guid id, UpdateProjectDto dto)
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.UpdateProject(id, dto, userId, HttpContext.RequestAborted));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = SystemRoles.Manager)]
        public async Task<IActionResult> Archive(Guid id)
        {
            var userId = _currentUser.GetRequiredUserId();
            await _service.ArchiveProject(id, userId, HttpContext.RequestAborted);
            return NoContent();
        }
    }
}
