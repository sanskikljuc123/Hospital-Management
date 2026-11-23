using api.Auth;
using api.Infrastructure;
using Domain.Constants;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.AppointmentService;

namespace api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Staff and patients only: an authenticated identity without a hospital role has no access.
    [Authorize(Roles = Roles.Admin + "," + Roles.Doctor + "," + Roles.Nurse + "," + Roles.Patient)]
    public class AppointmentController : HospitalControllerBase
    {
        private readonly IAppointmentService _appointments;
        private readonly IPatientAccessPolicy _access;

        public AppointmentController(IAppointmentService appointments, IPatientAccessPolicy access)
        {
            _appointments = appointments;
            _access = access;
        }

        /// <summary>Staff see all appointments (optionally filtered); patients only see their own.</summary>
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<AppointmentDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAppointments([FromQuery] int? patientId, [FromQuery] int? doctorId,
            [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
        {
            var scope = await _access.ListScopeAsync(StaffScope.Staff, cancellationToken);
            if (scope.IsEmpty) return Ok(Array.Empty<AppointmentDto>());
            if (!scope.AllPatients) patientId = scope.OnlyPatientId;

            return Ok(await _appointments.GetAllAsync(patientId, doctorId, from, to, cancellationToken));
        }

        [HttpGet("{id}")]
        [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAppointmentById([FromRoute] int id, CancellationToken cancellationToken)
        {
            var appointment = await _appointments.GetByIdAsync(id, cancellationToken);
            var decision = await _access.CheckAsync(appointment?.PatientId, StaffScope.Staff, cancellationToken);
            return decision == AccessDecision.Allow ? Ok(appointment) : Denied(decision, $"Appointment with ID {id} not found");
        }

        /// <summary>Staff can book for any patient; a patient can only book for themselves.</summary>
        [HttpPost]
        [ProducesResponseType<AppointmentDto>(StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateAppointment([FromBody] AppointmentCreateDto dto, CancellationToken cancellationToken)
        {
            // The patient id comes from the request: a patient may only name their own record.
            var decision = await _access.CheckAsync(dto.PatientId, StaffScope.Staff, cancellationToken);
            if (decision != AccessDecision.Allow) return Forbid();

            var result = await _appointments.CreateAsync(dto, cancellationToken);
            return result.Success
                ? CreatedAtAction(nameof(GetAppointmentById), new { id = result.Value!.Id }, result.Value)
                : FromResult(result);
        }

        [HttpPut("{id}")]
        [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
        [Authorize(Policy = Policies.Staff)]
        public async Task<IActionResult> UpdateAppointment([FromRoute] int id, [FromBody] AppointmentUpdateDto dto, CancellationToken cancellationToken) =>
            FromResult(await _appointments.UpdateAsync(id, dto, cancellationToken));

        /// <summary>Staff can cancel any appointment; a patient can cancel their own.</summary>
        [HttpPost("{id}/cancel")]
        [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> CancelAppointment([FromRoute] int id, CancellationToken cancellationToken)
        {
            var appointment = await _appointments.GetByIdAsync(id, cancellationToken);
            var decision = await _access.CheckAsync(appointment?.PatientId, StaffScope.Staff, cancellationToken);
            if (decision != AccessDecision.Allow) return Denied(decision, $"Appointment with ID {id} not found");

            return FromResult(await _appointments.CancelAsync(id, cancellationToken));
        }

        /// <summary>Admin only: corrects a final status set by mistake, with a mandatory reason.</summary>
        [HttpPost("{id}/correct-status")]
        [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> CorrectAppointmentStatus([FromRoute] int id,
            [FromBody] AppointmentStatusCorrectionDto dto, CancellationToken cancellationToken) =>
            FromResult(await _appointments.CorrectStatusAsync(id, dto, cancellationToken));

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> DeleteAppointment([FromRoute] int id, CancellationToken cancellationToken) =>
            await _appointments.DeleteAsync(id, cancellationToken)
                ? NoContent()
                : NotFound($"Appointment with ID {id} not found");
    }
}
