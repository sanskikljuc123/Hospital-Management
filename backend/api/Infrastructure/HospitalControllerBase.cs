using api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.Common;

namespace api.Infrastructure
{
    public abstract class HospitalControllerBase : ControllerBase
    {
        protected IActionResult FromResult<T>(ServiceResult<T> result) => result.Status switch
        {
            ResultStatus.Ok => Ok(result.Value),
            ResultStatus.NotFound => NotFound(result.Error),
            ResultStatus.Conflict => Conflict(result.Error),
            ResultStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result.Error),
            _ => BadRequest(result.Error)
        };

        /// <summary>The response for a refused <see cref="IPatientAccessPolicy"/> decision.</summary>
        protected IActionResult Denied(AccessDecision decision, string notFoundMessage) =>
            decision == AccessDecision.NotFound ? NotFound(notFoundMessage) : Forbid();
    }
}
