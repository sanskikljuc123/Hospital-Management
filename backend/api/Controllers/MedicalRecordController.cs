using api.Auth;
using api.Infrastructure;
using Domain.Constants;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.MedicalRecordService;
using Services.UserService;

namespace api.Controllers
{
    /// <summary>
    /// Clinical content. Doctors and nurses can read every record, patients only their own,
    /// and only doctors can write. Admins have no access to clinical content.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = Roles.Doctor + "," + Roles.Nurse + "," + Roles.Patient)]
    public class MedicalRecordController : HospitalControllerBase
    {
        private readonly IMedicalRecordService _records;
        private readonly IUserService _users;

        public MedicalRecordController(IMedicalRecordService records, IUserService users)
        {
            _records = records;
            _users = users;
        }

        private bool IsClinicalStaff => User.IsInRole(Roles.Doctor) || User.IsInRole(Roles.Nurse);

        [HttpGet]
        public async Task<IActionResult> GetMedicalRecords([FromQuery] int? patientId, CancellationToken cancellationToken)
        {
            if (!IsClinicalStaff)
            {
                patientId = await _users.GetCurrentPatientIdAsync(cancellationToken);
                if (patientId == null) return Ok(Array.Empty<MedicalRecordDto>());
            }

            return Ok(await _records.GetAllAsync(patientId, cancellationToken));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMedicalRecordById([FromRoute] int id, CancellationToken cancellationToken)
        {
            var record = await _records.GetByIdAsync(id, cancellationToken);
            // A patient gets the same refusal for a missing id as for someone else's, so ids cannot be probed.
            if (record == null) return IsClinicalStaff ? NotFound($"Medical record with ID {id} not found") : Forbid();
            if (!IsClinicalStaff && await _users.GetCurrentPatientIdAsync(cancellationToken) != record.PatientId)
                return Forbid();
            return Ok(record);
        }

        [HttpPost]
        [Authorize(Policy = Policies.DoctorOnly)]
        public async Task<IActionResult> CreateMedicalRecord([FromBody] MedicalRecordCreateDto dto, CancellationToken cancellationToken)
        {
            var result = await _records.CreateAsync(dto, cancellationToken);
            return result.Success
                ? CreatedAtAction(nameof(GetMedicalRecordById), new { id = result.Value!.Id }, result.Value)
                : FromResult(result);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = Policies.DoctorOnly)]
        public async Task<IActionResult> UpdateMedicalRecord([FromRoute] int id, [FromBody] MedicalRecordUpdateDto dto, CancellationToken cancellationToken) =>
            FromResult(await _records.UpdateAsync(id, dto, cancellationToken));
    }
}
