using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

        public UserController(IUserService service)
        {
            _service = service;
        }

        /// <summary>Lấy danh sách người dùng (Admin xem tất cả, Manager xem phòng mình)</summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> GetAll()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (role == "Manager")
            {
                if (!User.TryGetUserId(out var managerId))
                    return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

                return Ok(await _service.GetByManager(managerId, HttpContext.RequestAborted));
            }

            return Ok(await _service.GetAll(HttpContext.RequestAborted));
        }

        /// <summary>Tìm kiếm nhân viên theo tên, mã nhân viên, vai trò hoặc phòng ban</summary>
        [HttpGet("search")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Search(
            string? keyword,
            string? role,
            Guid? unitId)
        {
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            Guid? managerId = null;
            if (userRole == "Manager")
            {
                if (!User.TryGetUserId(out var mid))
                    return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

                managerId = mid;
            }

            var result = await _service.Search(
                keyword ?? "", role, unitId, managerId, HttpContext.RequestAborted);
            return Ok(result);
        }

        /// <summary>Cập nhật người dùng (chỉ Admin)</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, UpdateUserDto dto)
        {
            if (!User.TryGetUserId(out var changedBy))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.Update(id, dto, changedBy, HttpContext.RequestAborted));
        }

        /// <summary>Xóa người dùng (chỉ Admin)</summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!User.TryGetUserId(out var changedBy))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            await _service.Delete(id, changedBy, HttpContext.RequestAborted);
            return Ok();
        }

        /// <summary>Xem KPI cá nhân theo phạm vi được phân quyền</summary>
        [HttpGet("performance/{id}")]
        public async Task<IActionResult> GetPerformance(Guid id, Guid? periodId = null)
        {
            if (!User.TryGetUserId(out var currentUserId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var canView = await _service.CanViewPerformanceAsync(
                currentUserId,
                id,
                periodId,
                HttpContext.RequestAborted);
            if (canView)
                return Ok(await _service.GetPerformanceAsync(id, periodId, HttpContext.RequestAborted));

            return StatusCode(403, new { message = "Ban khong co quyen xem KPI cua nhan su nay.", code = "forbidden" });
        }

        /// <summary>Xem bảng KPI toàn phòng (Manager xem nhân viên phòng mình)</summary>
        [HttpGet("performance/unit")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetUnitPerformance(Guid? periodId = null)
        {
            if (!User.TryGetUserId(out var managerId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.GetUnitPerformanceAsync(managerId, periodId, HttpContext.RequestAborted));
        }
    }
}
