using api.Auth;
using api.Infrastructure;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.PatientService;
using Services.UserService;

namespace api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PatientController : HospitalControllerBase
    {
        private readonly IPatientService _patientService;
        private readonly IUserService _users;

        public PatientController(IPatientService patientService, IUserService users)
        {
            _patientService = patientService;
            _users = users;
        }

        [HttpGet]
        [Authorize(Policy = Policies.Staff)]
        public async Task<IActionResult> GetAllPatients()
        {
            var patients = await _patientService.GetAllPatients();
            return Ok(patients);
        }

        /// <summary>Staff can read any patient; a patient can only read their own record.</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPatientById([FromRoute] int id, CancellationToken cancellationToken)
        {
            if (!IsStaff && await _users.GetCurrentPatientIdAsync(cancellationToken) != id)
            {
                return Forbid();
            }

            var patient = await _patientService.GetPatientById(id);
            if (patient == null)
            {
                return NotFound($"Patient with ID {id} not found");
            }
            return Ok(patient);
        }

        [HttpPost]
        [Authorize(Policy = Policies.Staff)]
        public async Task<IActionResult> CreatePatient([FromBody] PatientCreationDto patientDto)
        {
            var (newPatient, res) = await _patientService.CreatePatient(patientDto);
            if (res)
            {
                return Ok(newPatient);
            }
            return BadRequest("Failed to create patient");
        }

        [HttpPut("{id}")]
        [Authorize(Policy = Policies.Staff)]
        public async Task<IActionResult> UpdatePatient([FromRoute] int id, [FromBody] PatientCreationDto patientDto)
        {
            var result = await _patientService.UpdatePatient(id, patientDto);
            if (result)
            {
                return Ok(new { patientDto, id });
            }
            return NotFound($"Patient with ID {id} not found");
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> DeletePatient([FromRoute] int id)
        {
            var result = await _patientService.DeletePatient(id);
            if (result)
            {
                return Ok($"Patient with ID {id} deleted successfully");
            }
            return NotFound($"Patient with ID {id} not found");
        }
    }

}
