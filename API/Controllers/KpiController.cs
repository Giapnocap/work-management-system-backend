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

        public KpiController(IKpiService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetPeriods()
            => Ok(await _service.GetPeriods(HttpContext.RequestAborted));

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent()
            => Ok(await _service.GetCurrentPeriod(HttpContext.RequestAborted));

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateKpiPeriodDto dto)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.CreatePeriod(dto, userId, HttpContext.RequestAborted));
        }

        [HttpPost("{id}/lock")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Lock(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.LockPeriod(id, userId, HttpContext.RequestAborted));
        }
    }
}
