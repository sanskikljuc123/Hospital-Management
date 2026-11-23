using api.Auth;
using api.Infrastructure;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.ServiceService;

namespace api.Controllers
{
    /// <summary>Medical services catalogue: readable by every hospital role, maintained by administrators.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ServiceController : HospitalControllerBase
    {
        private readonly IServiceService _services;

        public ServiceController(IServiceService services) => _services = services;

        [HttpGet]
        [ProducesResponseType<IReadOnlyList<ServiceDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllServices(CancellationToken cancellationToken) =>
            Ok(await _services.GetAllAsync(cancellationToken));

        [HttpGet("{id}")]
        [ProducesResponseType<ServiceDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetServiceById([FromRoute] int id, CancellationToken cancellationToken)
        {
            var service = await _services.GetByIdAsync(id, cancellationToken);
            return service == null ? NotFound($"Service with ID {id} not found") : Ok(service);
        }

        /// <summary>Binds a DTO, not the entity: extra fields such as patients or doctors are ignored.</summary>
        [HttpPost]
        [ProducesResponseType<ServiceDto>(StatusCodes.Status201Created)]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> CreateService([FromBody] ServiceDto dto, CancellationToken cancellationToken)
        {
            var result = await _services.CreateAsync(dto, cancellationToken);
            return result.Success
                ? CreatedAtAction(nameof(GetServiceById), new { id = result.Value!.Id }, result.Value)
                : FromResult(result);
        }

        [HttpPut("{id}")]
        [ProducesResponseType<ServiceDto>(StatusCodes.Status200OK)]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> UpdateService([FromRoute] int id, [FromBody] ServiceDto dto, CancellationToken cancellationToken) =>
            FromResult(await _services.UpdateAsync(id, dto, cancellationToken));

        /// <summary>204 when deleted; 409 while patients or doctors still use the service.</summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> DeleteService([FromRoute] int id, CancellationToken cancellationToken)
        {
            var result = await _services.DeleteAsync(id, cancellationToken);
            return result.Success ? NoContent() : FromResult(result);
        }
    }
}
