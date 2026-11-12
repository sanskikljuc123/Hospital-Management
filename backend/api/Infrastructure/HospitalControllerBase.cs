using Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using Services.Common;

namespace api.Infrastructure
{
    public abstract class HospitalControllerBase : ControllerBase
    {
        /// <summary>True for Admin, Doctor and Nurse; false for callers who are only patients.</summary>
        protected bool IsStaff =>
            User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Doctor) || User.IsInRole(Roles.Nurse);

        protected IActionResult FromResult<T>(ServiceResult<T> result) => result.Status switch
        {
            ResultStatus.Ok => Ok(result.Value),
            ResultStatus.NotFound => NotFound(result.Error),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => BadRequest(result.Error)
        };
    }
}
