using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/units")]
    public class UnitController : ControllerBase
    {
        private readonly IUnitService _service;
        private readonly ICurrentUserService _currentUser;

        public UnitController(IUnitService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<ActionResult<List<UnitDto>>> GetPublic()
            => Ok(await _service.GetAll(HttpContext.RequestAborted));

        [HttpGet]
        [Authorize(Roles = SystemRoles.AdminOrManager)]
        public async Task<ActionResult<List<UnitDto>>> GetAll()
            => Ok(await _service.GetAll(HttpContext.RequestAborted));

        [HttpGet("my-unit")]
        public async Task<ActionResult<UnitDto>> GetMyUnit()
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetMyUnit(userId, HttpContext.RequestAborted));
        }

        [HttpGet("{id}/users")]
        public async Task<ActionResult<List<UserDto>>> GetUsers(Guid id)
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetVisibleUsers(id, userId, HttpContext.RequestAborted));
        }

        [HttpPost]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<ActionResult<UnitDto>> Create(CreateUnitDto dto)
        {
            var changedBy = _currentUser.GetRequiredUserId();
            var result = await _service.Create(dto, changedBy, HttpContext.RequestAborted);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<ActionResult<UnitDto>> Update(Guid id, UpdateUnitDto dto)
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

        [HttpPost("{id}/members")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<IActionResult> AddMember(Guid id, [FromBody] MemberDto dto)
        {
            var userId = _currentUser.GetRequiredUserId();
            await _service.AddMemberForRequester(id, dto.UserId, userId, HttpContext.RequestAborted);
            return NoContent();
        }

        [HttpDelete("{id}/members/{userId}")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
        {
            var currentUserId = _currentUser.GetRequiredUserId();
            await _service.RemoveMemberForRequester(id, userId, currentUserId, HttpContext.RequestAborted);
            return NoContent();
        }
    }
}
