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

        /// <summary>Lấy danh sách người dùng (Admin xem tất cả, Manager xem phòng mình)</summary>
        [HttpGet]
        [Authorize(Roles = SystemRoles.AdminOrManager)]
        public async Task<ActionResult<List<UserDto>>> GetAll()
        {
            var requesterId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetVisibleUsers(requesterId, HttpContext.RequestAborted));
        }

        /// <summary>Tìm kiếm nhân viên theo tên, mã nhân viên, vai trò hoặc phòng ban</summary>
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

        /// <summary>Cập nhật người dùng (chỉ Admin)</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserDto dto)
        {
            var changedBy = _currentUser.GetRequiredUserId();
            return Ok(await _service.Update(id, dto, changedBy, HttpContext.RequestAborted));
        }

        /// <summary>Xóa người dùng (chỉ Admin)</summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = SystemRoles.Admin)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var changedBy = _currentUser.GetRequiredUserId();
            await _service.Delete(id, changedBy, HttpContext.RequestAborted);
            return NoContent();
        }

        /// <summary>Xem KPI cá nhân theo phạm vi được phân quyền</summary>
        [HttpGet("performance/{id}")]
        public async Task<ActionResult<PerformanceDto>> GetPerformance(Guid id, Guid? periodId = null)
        {
            var currentUserId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetVisiblePerformanceAsync(
                currentUserId, id, periodId, HttpContext.RequestAborted));
        }

        /// <summary>Xem bảng KPI toàn phòng (Manager xem nhân viên phòng mình)</summary>
        [HttpGet("performance/unit")]
        [Authorize(Roles = SystemRoles.ManagerOrAdmin)]
        public async Task<ActionResult<List<PerformanceDto>>> GetUnitPerformance(Guid? periodId = null)
        {
            var managerId = _currentUser.GetRequiredUserId();
            return Ok(await _service.GetUnitPerformanceAsync(managerId, periodId, HttpContext.RequestAborted));
        }
    }
}
