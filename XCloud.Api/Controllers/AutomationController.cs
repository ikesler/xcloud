using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using XCloud.Automations.Api;

namespace XCloud.Api.Controllers;

[Route("automation")]
[Authorize]
[ApiController]
public class AutomationController(IAutomationManager automationManager) : ControllerBase
{
    [HttpPost]
    [Route("{automation}")]
    public IActionResult RunOne([FromRoute] string automation, [FromBody] Dictionary<string, string>? args)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (args == null)
                {
                    await automationManager.Run(automation);
                }
                else
                {
                    await automationManager.Run(automation, args);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "An error occurred when running {Automation} automation with {Parameters} parameters", automation, args);
            }
        });
        return Ok(new { message = "Automation started but not awaited" });
    }

    [HttpPost]
    [Route("")]
    public IActionResult RunAll()
    {
        _ = Task.Run(automationManager.RunAll);
        return Ok(new { message = "Automations started but not awaited" });
    }
}
