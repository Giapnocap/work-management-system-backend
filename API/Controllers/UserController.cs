using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _service;
        private readonly ICurrentUserService _currentUser;

        public UserController(IUserService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        [HttpGet]
        [Authorize(Roles = SystemRoles.AdminOrManager)]
        public async Task<ActionResult<List<UserDto>>> GetAll()
        {
            var requesterId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetVisibleUsers(requesterId, HttpContext.RequestAborted));
        }

        [HttpGet("search")]
        [Authorize(Roles = SystemRoles.AdminOrManager)]
        public async Task<ActionResult<List<UserDto>>> Search(
            string? keyword,
            string? role,
            Guid? unitId)
        {
            var requesterId = _currentUser.GetRequiredUserId();
            var result = await _service.SearchVisibleUsers(
                requesterId, keyword ?? "", role, unitId, HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserDto dto)
        {
            var changedBy = _currentUser.GetRequiredUserId();
            return Ok(await _service.Update(id, dto, changedBy, HttpContext.RequestAborted));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var changedBy = _currentUser.GetRequiredUserId();
            await _service.Delete(id, changedBy, HttpContext.RequestAborted);
            return NoContent();
        }

        [HttpGet("performance/{id}")]
        public async Task<ActionResult<PerformanceDto>> GetPerformance(Guid id, Guid? periodId = null)
        {
            var currentUserId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetVisiblePerformanceAsync(
                currentUserId, id, periodId, HttpContext.RequestAborted));
        }

        [HttpGet("performance/unit")]
        [Authorize(Roles = SystemRoles.ManagerOrAdmin)]
        public async Task<ActionResult<List<PerformanceDto>>> GetUnitPerformance(Guid? periodId = null)
        {
            var managerId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetUnitPerformanceAsync(managerId, periodId, HttpContext.RequestAborted));
        }
    }
}
