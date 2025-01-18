using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using XCloud.Automations.Api;
using XCloud.Automations.Impl;
using XCloud.Automations.Impl.AiNoteTitle;
using XCloud.Automations.Jobs;
using XCloud.Automations.Settings;
using XCloud.Core;
using XCloud.Ext.Automation;

namespace XCloud.Automations;

public class Module: IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IAutomation, AiTitleAutomation>();
        services.AddTransient<IAutomationManager, AutomationManager>();
        services.AddOptions<Dictionary<string, AiSettings>>().Bind(configuration.GetSection("OpenAiEndpoints"));
        services.AddOptions<AiTitleAutomationSettings>()
            .Bind(configuration.GetSection($"Automations:{AiTitleAutomation.AutomationCode}"));

        services.AddQuartz(q =>
        {
            var jobKey = new JobKey(nameof(AiTitleJob));
            q.AddJob<AiTitleJob>(opts => opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{nameof(AiTitleJob)}-trigger")
                .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(2, 33))
            );
        });
        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });
    }
}
