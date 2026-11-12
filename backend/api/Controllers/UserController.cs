using api.Auth;
using api.Infrastructure;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.UserService;

namespace api.Controllers
{
    /// <summary>
    /// Local user profiles. Credentials, password resets and role assignments are managed in
    /// Keycloak; this API manages what the hospital system adds on top (activation and links
    /// to doctor/patient records).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : HospitalControllerBase
    {
        private readonly IUserService _users;

        public UserController(IUserService users) => _users = users;

        /// <summary>The caller's own profile, as mirrored from their token.</summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
        {
            var user = await _users.GetCurrentAsync(cancellationToken);
            return user == null ? NotFound("No profile exists for the current user") : Ok(user);
        }

        [HttpGet]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken) =>
            Ok(await _users.GetAllAsync(cancellationToken));

        [HttpGet("{id}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> GetUserById([FromRoute] int id, CancellationToken cancellationToken)
        {
            var user = await _users.GetByIdAsync(id, cancellationToken);
            return user == null ? NotFound($"User with ID {id} not found") : Ok(user);
        }

        /// <summary>Activates or deactivates an account. Deactivated users get 403 on every request.</summary>
        [HttpPut("{id}/status")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> SetUserStatus([FromRoute] int id, [FromBody] UserStatusDto dto, CancellationToken cancellationToken) =>
            FromResult(await _users.SetActiveAsync(id, dto.IsActive, cancellationToken));

        /// <summary>Links the account to a doctor and/or patient record; this is what lets a patient see their own data.</summary>
        [HttpPut("{id}/link")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> LinkUser([FromRoute] int id, [FromBody] UserLinkDto dto, CancellationToken cancellationToken) =>
            FromResult(await _users.LinkAsync(id, dto, cancellationToken));
    }
}
