using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/kpi-periods")]
    public class KpiController : ControllerBase
    {
        private readonly IKpiService _service;
        private readonly ICurrentUserService _currentUser;

        public KpiController(IKpiService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<ActionResult<List<KpiPeriodDto>>> GetPeriods()
            => Ok(await _service.GetPeriods(HttpContext.RequestAborted));

        [HttpGet("current")]
        public async Task<ActionResult<KpiPeriodDto>> GetCurrent()
            => Ok(await _service.GetCurrentPeriod(HttpContext.RequestAborted));

        [HttpPost]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<ActionResult<KpiPeriodDto>> Create(CreateKpiPeriodDto dto)
        {
            var userId = _currentUser.GetRequiredUserId();
            var result = await _service.CreatePeriod(dto, userId, HttpContext.RequestAborted);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpPost("{id}/lock")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<ActionResult<List<PerformanceDto>>> Lock(Guid id)
        {
            var userId = _currentUser.GetRequiredUserId();
            return Ok(await _service.LockPeriod(id, userId, HttpContext.RequestAborted));
        }
    }
}
