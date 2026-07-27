using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/audit-logs")]
    public sealed class AuditController : ControllerBase
    {
        private readonly IAuditService _auditService;

        public AuditController(IAuditService auditService)
        {
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            string? entityType,
            Guid? entityId,
            string? action,
            Guid? actorUserId,
            DateTime? from,
            DateTime? to,
            int page = 1,
            int size = 20)
        {
            return Ok(await _auditService.GetAsync(
                entityType,
                entityId,
                action,
                actorUserId,
                from,
                to,
                page,
                size,
                HttpContext.RequestAborted));
        }
    }
}
