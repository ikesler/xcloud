using Microsoft.AspNetCore.Mvc;

namespace XCloud.Api.Controllers;
public class ErrorController : Controller
{
    [Route("/error/{code:int}")]
    public Task<IActionResult> GetErrorPage(int code)
    {
        return Task.FromResult<IActionResult>(View("NotFound"));
    }
}
