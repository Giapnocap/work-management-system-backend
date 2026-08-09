using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _service;
        private readonly ICurrentUserService _currentUser;

        public ProfileController(IProfileService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<ActionResult<ProfileDto>> GetProfile(CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetProfile(userId, cancellationToken));
        }

        [HttpPut]
        public async Task<ActionResult<ProfileDto>> UpdateProfile(
            [FromBody] ProfileDto dto,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.UpdateProfile(userId, dto, cancellationToken));
        }
    }
}
