using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize(Roles = SystemRoles.AdminOrManager)]
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _service;
        private readonly ICurrentUserService _currentUser;

        public DashboardController(IDashboardService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Lấy thống kê tổng quan (Admin)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<ActionResult<DashboardDto>> GetDashboard(CancellationToken cancellationToken)
            => Ok(await _service.GetDashboard(cancellationToken));

        /// <summary>
        /// Lấy thống kê phòng ban (Manager)
        /// </summary>
        [HttpGet("manager")]
        [Authorize(Roles = SystemRoles.Manager)]
        public async Task<ActionResult<ManagerDashboardDto>> GetManagerDashboard(CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            var result = await _service.GetManagerDashboard(userId, cancellationToken);
            return Ok(result);
        }
    }
}
