using Aenaon.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Aenaon.Api.Controllers;

[ApiController]
[Route("api/info")]
public sealed class InfoController : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiInfoResponse> Get()
    {
        return Ok(new ApiInfoResponse("Aenaon ERP API"));
    }
}
