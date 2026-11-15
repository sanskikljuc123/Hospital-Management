using api.Auth;
using api.Infrastructure;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DoctorService;

namespace api.Controllers
{
    /// <summary>Doctor directory: readable by every hospital role, maintained by administrators.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DoctorController : HospitalControllerBase
    {
        private readonly IDoctorService _doctors;

        public DoctorController(IDoctorService doctors) => _doctors = doctors;

        [HttpGet]
        public async Task<IActionResult> GetAllDoctors(CancellationToken cancellationToken) =>
            Ok(await _doctors.GetAllAsync(cancellationToken));

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDoctorById([FromRoute] int id, CancellationToken cancellationToken)
        {
            var doctor = await _doctors.GetByIdAsync(id, cancellationToken);
            return doctor == null ? NotFound($"Doctor with ID {id} not found") : Ok(doctor);
        }

        [HttpPost]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> CreateDoctor([FromBody] DoctorCreationDto dto, CancellationToken cancellationToken)
        {
            var result = await _doctors.CreateAsync(dto, cancellationToken);
            return result.Success
                ? CreatedAtAction(nameof(GetDoctorById), new { id = result.Value!.Id }, result.Value)
                : FromResult(result);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> UpdateDoctor([FromRoute] int id, [FromBody] DoctorCreationDto dto, CancellationToken cancellationToken) =>
            FromResult(await _doctors.UpdateAsync(id, dto, cancellationToken));

        /// <summary>204 when deleted; 409 while the doctor has appointments or medical records.</summary>
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> DeleteDoctor([FromRoute] int id, CancellationToken cancellationToken) =>
            await _doctors.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound($"Doctor with ID {id} not found");
    }
}
