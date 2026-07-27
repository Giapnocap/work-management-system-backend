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
        private readonly ITaskAccessService _accessService;

        public UnitController(IUnitService service, ITaskAccessService accessService)
        {
            _service = service;
            _accessService = accessService;
        }

        /// <summary>Lấy danh sách phòng ban công khai (dùng cho trang đăng ký)</summary>
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublic()
            => Ok(await _service.GetAll(HttpContext.RequestAborted));

        /// <summary>Lấy danh sách tất cả đơn vị (Admin + Manager)</summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> GetAll()
            => Ok(await _service.GetAll(HttpContext.RequestAborted));

        /// <summary>Lấy đơn vị của user đang đăng nhập</summary>
        [HttpGet("my-unit")]
        public async Task<IActionResult> GetMyUnit()
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var unit = await _service.GetMyUnit(userId, HttpContext.RequestAborted);
            if (unit == null) return NotFound(new { message = "Ban chua thuoc don vi nao.", code = "not_found" });
            return Ok(unit);
        }

        /// <summary>Lấy danh sách thành viên trong đơn vị</summary>
        [HttpGet("{id}/users")]
        public async Task<IActionResult> GetUsers(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            var role = await _accessService.GetUserRole(userId);
            var userUnitId = await _accessService.GetUserUnitId(userId);

            if (role != "Admin" && userUnitId != id && !await _accessService.CanManageUnit(id, userId))
                return StatusCode(403, new { message = "Ban khong co quyen xem thanh vien phong ban nay.", code = "forbidden" });

            return Ok(await _service.GetUsers(id, HttpContext.RequestAborted));
        }

        /// <summary>Tạo đơn vị mới (chỉ Admin)</summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateUnitDto dto)
        {
            if (!User.TryGetUserId(out var changedBy))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.Create(dto, changedBy, HttpContext.RequestAborted));
        }

        /// <summary>Cập nhật đơn vị (chỉ Admin)</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, CreateUnitDto dto)
        {
            if (!User.TryGetUserId(out var changedBy))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            return Ok(await _service.Update(id, dto, changedBy, HttpContext.RequestAborted));
        }

        /// <summary>Xóa đơn vị (chỉ Admin)</summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!User.TryGetUserId(out var changedBy))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            await _service.Delete(id, changedBy, HttpContext.RequestAborted);
            return Ok(new { message = "Deleted successfully" });
        }

        /// <summary>Thêm thành viên vào đơn vị (Admin)</summary>
        [HttpPost("{id}/members")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddMember(Guid id, [FromBody] MemberDto dto)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            if (!await _accessService.CanManageUnit(id, userId))
                return StatusCode(403, new { message = "Ban khong co quyen them thanh vien vao phong ban nay.", code = "forbidden" });

            await _service.AddMember(id, dto.UserId, userId, HttpContext.RequestAborted);
            return Ok(new { message = "Đã thêm thành viên!" });
        }

        /// <summary>Xóa thành viên khỏi đơn vị (Admin)</summary>
        [HttpDelete("{id}/members/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
        {
            if (!User.TryGetUserId(out var currentUserId))
                return Unauthorized(new { message = "Khong xac dinh duoc nguoi dung.", code = "unauthorized" });

            if (!await _accessService.CanManageUnit(id, currentUserId))
                return StatusCode(403, new { message = "Ban khong co quyen xoa thanh vien khoi phong ban nay.", code = "forbidden" });

            await _service.RemoveMember(id, userId, currentUserId, HttpContext.RequestAborted);
            return Ok(new { message = "Đã xóa thành viên!" });
        }
    }
}
