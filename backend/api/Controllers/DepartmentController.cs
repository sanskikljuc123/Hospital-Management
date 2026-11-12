using api.Auth;
using api.Infrastructure;
using Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DepartmentService;

namespace api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DepartmentController : HospitalControllerBase
    {
        private readonly IDepartmentService _departments;

        public DepartmentController(IDepartmentService departments) => _departments = departments;

        [HttpGet]
        public async Task<IActionResult> GetAllDepartments(CancellationToken cancellationToken) =>
            Ok(await _departments.GetAllAsync(cancellationToken));

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDepartmentById([FromRoute] int id, CancellationToken cancellationToken)
        {
            var department = await _departments.GetByIdAsync(id, cancellationToken);
            return department == null ? NotFound($"Department with ID {id} not found") : Ok(department);
        }

        [HttpPost]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> CreateDepartment([FromBody] DepartmentDto dto, CancellationToken cancellationToken)
        {
            var result = await _departments.CreateAsync(dto, cancellationToken);
            return result.Success
                ? CreatedAtAction(nameof(GetDepartmentById), new { id = result.Value!.Id }, result.Value)
                : FromResult(result);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> UpdateDepartment([FromRoute] int id, [FromBody] DepartmentDto dto, CancellationToken cancellationToken) =>
            FromResult(await _departments.UpdateAsync(id, dto, cancellationToken));

        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> DeleteDepartment([FromRoute] int id, CancellationToken cancellationToken) =>
            await _departments.DeleteAsync(id, cancellationToken)
                ? NoContent()
                : NotFound($"Department with ID {id} not found");
    }
}
