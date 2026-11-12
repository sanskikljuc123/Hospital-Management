using api.Auth;
using api.Infrastructure;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.AppointmentService;
using Services.UserService;

namespace api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AppointmentController : HospitalControllerBase
    {
        private readonly IAppointmentService _appointments;
        private readonly IUserService _users;

        public AppointmentController(IAppointmentService appointments, IUserService users)
        {
            _appointments = appointments;
            _users = users;
        }

        /// <summary>Staff see all appointments (optionally filtered); patients only see their own.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAppointments([FromQuery] int? patientId, [FromQuery] int? doctorId,
            [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
        {
            if (!IsStaff)
            {
                patientId = await _users.GetCurrentPatientIdAsync(cancellationToken);
                if (patientId == null) return Ok(Array.Empty<AppointmentDto>());
            }

            return Ok(await _appointments.GetAllAsync(patientId, doctorId, from, to, cancellationToken));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointmentById([FromRoute] int id, CancellationToken cancellationToken)
        {
            var appointment = await _appointments.GetByIdAsync(id, cancellationToken);
            if (appointment == null) return NotFound($"Appointment with ID {id} not found");
            if (!await CanAccessPatientAsync(appointment.PatientId, cancellationToken)) return Forbid();
            return Ok(appointment);
        }

        /// <summary>Staff can book for any patient; a patient can only book for themselves.</summary>
        [HttpPost]
        public async Task<IActionResult> CreateAppointment([FromBody] AppointmentCreateDto dto, CancellationToken cancellationToken)
        {
            if (!await CanAccessPatientAsync(dto.PatientId, cancellationToken)) return Forbid();

            var result = await _appointments.CreateAsync(dto, cancellationToken);
            return result.Success
                ? CreatedAtAction(nameof(GetAppointmentById), new { id = result.Value!.Id }, result.Value)
                : FromResult(result);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = Policies.Staff)]
        public async Task<IActionResult> UpdateAppointment([FromRoute] int id, [FromBody] AppointmentUpdateDto dto, CancellationToken cancellationToken) =>
            FromResult(await _appointments.UpdateAsync(id, dto, cancellationToken));

        /// <summary>Staff can cancel any appointment; a patient can cancel their own.</summary>
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelAppointment([FromRoute] int id, CancellationToken cancellationToken)
        {
            var appointment = await _appointments.GetByIdAsync(id, cancellationToken);
            if (appointment == null) return NotFound($"Appointment with ID {id} not found");
            if (!await CanAccessPatientAsync(appointment.PatientId, cancellationToken)) return Forbid();

            return FromResult(await _appointments.CancelAsync(id, cancellationToken));
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> DeleteAppointment([FromRoute] int id, CancellationToken cancellationToken) =>
            await _appointments.DeleteAsync(id, cancellationToken)
                ? NoContent()
                : NotFound($"Appointment with ID {id} not found");

        private async Task<bool> CanAccessPatientAsync(int patientId, CancellationToken cancellationToken) =>
            IsStaff || await _users.GetCurrentPatientIdAsync(cancellationToken) == patientId;
    }
}
