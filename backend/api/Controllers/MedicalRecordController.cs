using api.Auth;
using api.Infrastructure;
using Domain.Constants;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        private readonly IPatientAccessPolicy _access;

        public MedicalRecordController(IMedicalRecordService records, IUserService users, IPatientAccessPolicy access)
        {
            _records = records;
            _users = users;
            _access = access;
        }

        /// <summary>Doctors and nurses see all records (optionally filtered); patients only their own.</summary>
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<MedicalRecordDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMedicalRecords([FromQuery] int? patientId, CancellationToken cancellationToken)
        {
            var scope = await _access.ListScopeAsync(StaffScope.ClinicalStaff, cancellationToken);
            if (scope.IsEmpty) return Ok(Array.Empty<MedicalRecordDto>());
            if (!scope.AllPatients) patientId = scope.OnlyPatientId;

            return Ok(await _records.GetAllAsync(patientId, cancellationToken));
        }

        [HttpGet("{id}")]
        [ProducesResponseType<MedicalRecordDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<IActionResult> GetMedicalRecordById([FromRoute] int id, CancellationToken cancellationToken)
        {
            var record = await _records.GetByIdAsync(id, cancellationToken);
            var decision = await _access.CheckAsync(record?.PatientId, StaffScope.ClinicalStaff, cancellationToken);
            return decision == AccessDecision.Allow ? Ok(record) : Denied(decision, $"Medical record with ID {id} not found");
        }

        [HttpPost]
        [ProducesResponseType<MedicalRecordDto>(StatusCodes.Status201Created)]
        [Authorize(Policy = Policies.DoctorOnly)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<IActionResult> CreateMedicalRecord([FromBody] MedicalRecordCreateDto dto, CancellationToken cancellationToken)
        {
            // The author comes from the caller's account, so a doctor cannot sign as someone else.
            var doctorId = await _users.GetCurrentDoctorIdAsync(cancellationToken);
            if (doctorId == null)
                return ForbiddenProblem("This account is not linked to a doctor profile");

            var result = await _records.CreateAsync(dto, doctorId.Value, cancellationToken);
            return result.Success
                ? CreatedAtAction(nameof(GetMedicalRecordById), new { id = result.Value!.Id }, result.Value)
                : FromResult(result);
        }

        /// <summary>Any doctor may edit any record; every change is kept as a new revision with its editor.</summary>
        [HttpPut("{id}")]
        [ProducesResponseType<MedicalRecordDto>(StatusCodes.Status200OK)]
        [Authorize(Policy = Policies.DoctorOnly)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<IActionResult> UpdateMedicalRecord([FromRoute] int id, [FromBody] MedicalRecordUpdateDto dto, CancellationToken cancellationToken)
        {
            var doctorId = await _users.GetCurrentDoctorIdAsync(cancellationToken);
            if (doctorId == null)
                return ForbiddenProblem("This account is not linked to a doctor profile");

            return FromResult(await _records.UpdateAsync(id, dto, doctorId.Value, cancellationToken));
        }

        /// <summary>Every version of the record with who changed what and when. Doctors and nurses only.</summary>
        [HttpGet("{id}/history")]
        [ProducesResponseType<IReadOnlyList<MedicalRecordRevisionDto>>(StatusCodes.Status200OK)]
        [Authorize(Policy = Policies.ClinicalStaff)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<IActionResult> GetMedicalRecordHistory([FromRoute] int id, CancellationToken cancellationToken)
        {
            var history = await _records.GetHistoryAsync(id, cancellationToken);
            return history == null ? NotFoundProblem($"Medical record with ID {id} not found") : Ok(history);
        }
    }
}
