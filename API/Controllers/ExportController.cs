using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize(Roles = SystemRoles.AdminOrManager)]
    [ApiController]
    [Route("api/export")]
    public class ExportController : ControllerBase
    {
        private readonly IExportService _service;
        private readonly ICurrentUserService _currentUser;

        public ExportController(IExportService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Export danh sách công việc ra Excel
        /// </summary>
        [HttpGet("tasks")]
        public async Task<IActionResult> ExportTasks(CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            var bytes = await _service.ExportTasksToExcel(userId, cancellationToken);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhSachCongViec_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }

        /// <summary>
        /// Export tiến độ công việc ra Excel
        /// </summary>
        [HttpGet("progress")]
        public async Task<IActionResult> ExportProgress(CancellationToken cancellationToken)
        {
            var userId = _currentUser.GetRequiredUserId();
            var bytes = await _service.ExportProgressToExcel(userId, cancellationToken);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"TienDoCongViec_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }
    }
}
