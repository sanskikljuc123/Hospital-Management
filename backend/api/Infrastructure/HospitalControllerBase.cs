using api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.Common;

namespace api.Infrastructure
{
    /// <summary>
    /// Error responses (docs/API_ENDPOINTS.md, Common responses): every 400, 404 and 409, and a 403 that has a reason
    /// to give, is a ProblemDetails body (<c>application/problem+json</c>) with the message in <c>detail</c>. A 403 from
    /// an ownership or role check (<see cref="Denied"/>, <c>[Authorize]</c>) stays empty on purpose, so it reveals
    /// nothing about the record (R-004).
    /// </summary>
    public abstract class HospitalControllerBase : ControllerBase
    {
        protected IActionResult FromResult<T>(ServiceResult<T> result) => result.Status switch
        {
            ResultStatus.Ok => Ok(result.Value),
            ResultStatus.NotFound => Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound),
            ResultStatus.Conflict => Problem(detail: result.Error, statusCode: StatusCodes.Status409Conflict),
            ResultStatus.Forbidden => Problem(detail: result.Error, statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest)
        };

        /// <summary>404 as ProblemDetails, with <paramref name="detail"/> as the message.</summary>
        protected IActionResult NotFoundProblem(string detail) =>
            Problem(detail: detail, statusCode: StatusCodes.Status404NotFound);

        /// <summary>403 as ProblemDetails, for a refusal that explains itself (not an ownership check).</summary>
        protected IActionResult ForbiddenProblem(string detail) =>
            Problem(detail: detail, statusCode: StatusCodes.Status403Forbidden);

        /// <summary>The response for a refused <see cref="IPatientAccessPolicy"/> decision.</summary>
        protected IActionResult Denied(AccessDecision decision, string notFoundMessage) =>
            decision == AccessDecision.NotFound ? NotFoundProblem(notFoundMessage) : Forbid();
    }
}
