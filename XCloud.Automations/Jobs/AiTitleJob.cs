using Quartz;
using XCloud.Automations.Api;
using XCloud.Automations.Impl.AiNoteTitle;

namespace XCloud.Automations.Jobs;

[DisallowConcurrentExecution]
public class AiTitleJob(IAutomationManager automationManager): IJob
{
    public Task Execute(IJobExecutionContext context) => automationManager.Run(AiTitleAutomation.AutomationCode);
}
