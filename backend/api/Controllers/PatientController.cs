using api.Auth;
using api.Infrastructure;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.PatientService;

namespace api.Controllers
{
    /// <summary>
    /// Patient registry: staff read it; a nurse or an administrator registers and edits patients (F-02); only an
    /// administrator deletes; a patient can read only their own record.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PatientController : HospitalControllerBase
    {
        private readonly IPatientService _patients;
        private readonly IPatientAccessPolicy _access;

        public PatientController(IPatientService patients, IPatientAccessPolicy access)
        {
            _patients = patients;
            _access = access;
        }

        [HttpGet]
        [ProducesResponseType<IReadOnlyList<PatientDto>>(StatusCodes.Status200OK)]
        [Authorize(Policy = Policies.Staff)]
        public async Task<IActionResult> GetAllPatients(CancellationToken cancellationToken) =>
            Ok(await _patients.GetAllAsync(cancellationToken));

        /// <summary>Staff can read any patient; a patient only their own (403 for any other id, existing or not).</summary>
        [HttpGet("{id}")]
        [ProducesResponseType<PatientDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<IActionResult> GetPatientById([FromRoute] int id, CancellationToken cancellationToken)
        {
            var patient = await _patients.GetByIdAsync(id, cancellationToken);
            var decision = await _access.CheckAsync(patient?.Id, StaffScope.Staff, cancellationToken);
            return decision == AccessDecision.Allow ? Ok(patient) : Denied(decision, $"Patient with ID {id} not found");
        }

        [HttpPost]
        [ProducesResponseType<PatientDto>(StatusCodes.Status201Created)]
        [Authorize(Policy = Policies.PatientRegistration)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> CreatePatient([FromBody] PatientCreationDto dto, CancellationToken cancellationToken)
        {
            var result = await _patients.CreateAsync(dto, cancellationToken);
            return result.Success
                ? CreatedAtAction(nameof(GetPatientById), new { id = result.Value!.Id }, result.Value)
                : FromResult(result);
        }

        [HttpPut("{id}")]
        [ProducesResponseType<PatientDto>(StatusCodes.Status200OK)]
        [Authorize(Policy = Policies.PatientRegistration)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<IActionResult> UpdatePatient([FromRoute] int id, [FromBody] PatientCreationDto dto, CancellationToken cancellationToken) =>
            FromResult(await _patients.UpdateAsync(id, dto, cancellationToken));

        /// <summary>204 when deleted; 409 while the patient has appointments or medical records.</summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [Authorize(Policy = Policies.AdminOnly)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<IActionResult> DeletePatient([FromRoute] int id, CancellationToken cancellationToken) =>
            await _patients.DeleteAsync(id, cancellationToken) ? NoContent() : NotFoundProblem($"Patient with ID {id} not found");
    }
}
