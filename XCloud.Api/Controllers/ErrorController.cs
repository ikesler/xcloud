using Microsoft.AspNetCore.Mvc;

namespace XCloud.Api.Controllers;
[Route("error")]
public class ErrorController : Controller
{
    [Route("{code:int}")]
    public Task<IActionResult> GetErrorPage(int code)
    {
        return Task.FromResult<IActionResult>(View("NotFound"));
    }
}
