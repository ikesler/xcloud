using XCloud.Automations.Api;
using XCloud.Core;
using XCloud.Ext.Automation;
using XCloud.Storage.Api;

namespace XCloud.Automations.Impl;

public class AutomationManager(IStorage storage, IEnumerable<IAutomation> automations)
    : IAutomationManager
{
    public async Task RunAll()
    {
        var settings = await storage.LoadSettings();
        foreach (var automationSettings in settings.Automations)
        {
            var automation = automations.FirstOrDefault(x => x.Code == automationSettings.Type);
            if (automation == null) throw new XCloudException($"Could not find automation: {automationSettings.Type}");
            await automation.Run(automationSettings.Params);
        }
    }

    public async Task Run(string code)
    {
        var settings = await storage.LoadSettings();
        foreach (var automationSettings in settings.Automations.Where(x => x.Type == code))
        {
            await Run(code, automationSettings.Params);
        }
    }

    public async Task Run(string code, Dictionary<string, string> args)
    {
        var automation = automations.FirstOrDefault(x => x.Code == code);
        if (automation == null) throw new XCloudException($"Could not find automation: {code}");
        await automation.Run(args);
    }
}
