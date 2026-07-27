using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize(Roles = "Manager")]
    [ApiController]
    [Route("api/projects")]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectService _service;

        public ProjectController(IProjectService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetProjects()
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.GetProjects(userId, HttpContext.RequestAborted));
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Create(CreateProjectDto dto)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.CreateProject(dto, userId, HttpContext.RequestAborted));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Update(Guid id, CreateProjectDto dto)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.UpdateProject(id, dto, userId, HttpContext.RequestAborted));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Archive(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            await _service.ArchiveProject(id, userId, HttpContext.RequestAborted);
            return Ok(new { message = "Archived successfully" });
        }
    }
}
