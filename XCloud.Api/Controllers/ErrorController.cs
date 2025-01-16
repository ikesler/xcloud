using Microsoft.AspNetCore.Mvc;

namespace XCloud.Api.Controllers;
public class ErrorController : Controller
{
    [Route("/error/{code:int}")]
    public async Task<IActionResult> GetErrorPage(int code)
    {
        return View("NotFound");
    }
}
