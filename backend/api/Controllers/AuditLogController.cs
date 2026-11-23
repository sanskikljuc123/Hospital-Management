using api.Auth;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.AuditService;

namespace api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = Policies.AdminOnly)]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditService _audit;

        public AuditLogController(IAuditService audit) => _audit = audit;

        /// <summary>Newest first. Filter by action, user (Keycloak subject), entity and time range.</summary>
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<AuditLogDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogQuery query, CancellationToken cancellationToken) =>
            Ok(await _audit.QueryAsync(query, cancellationToken));
    }
}
