using api.Auth;
using api.Infrastructure;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.PatientService;
using Services.UserService;

namespace api.Controllers
{
    /// <summary>Patient registry: staff manage it; a patient can read only their own record.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PatientController : HospitalControllerBase
    {
        private readonly IPatientService _patients;
        private readonly IUserService _users;

        public PatientController(IPatientService patients, IUserService users)
        {
            _patients = patients;
            _users = users;
        }

        [HttpGet]
        [Authorize(Policy = Policies.Staff)]
        public async Task<IActionResult> GetAllPatients(CancellationToken cancellationToken) =>
            Ok(await _patients.GetAllAsync(cancellationToken));

        /// <summary>Staff can read any patient; a patient only their own (403 for any other id, existing or not).</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPatientById([FromRoute] int id, CancellationToken cancellationToken)
        {
            if (!IsStaff && await _users.GetCurrentPatientIdAsync(cancellationToken) != id)
                return Forbid();

            var patient = await _patients.GetByIdAsync(id, cancellationToken);
            return patient == null ? NotFound($"Patient with ID {id} not found") : Ok(patient);
        }

        [HttpPost]
        [Authorize(Policy = Policies.Staff)]
        public async Task<IActionResult> CreatePatient([FromBody] PatientCreationDto dto, CancellationToken cancellationToken)
        {
            var result = await _patients.CreateAsync(dto, cancellationToken);
            return result.Success
                ? CreatedAtAction(nameof(GetPatientById), new { id = result.Value!.Id }, result.Value)
                : FromResult(result);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = Policies.Staff)]
        public async Task<IActionResult> UpdatePatient([FromRoute] int id, [FromBody] PatientCreationDto dto, CancellationToken cancellationToken) =>
            FromResult(await _patients.UpdateAsync(id, dto, cancellationToken));

        /// <summary>204 when deleted; 409 while the patient has appointments or medical records.</summary>
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> DeletePatient([FromRoute] int id, CancellationToken cancellationToken) =>
            await _patients.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound($"Patient with ID {id} not found");
    }
}
