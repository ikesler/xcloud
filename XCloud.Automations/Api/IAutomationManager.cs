namespace XCloud.Automations.Api;

public interface IAutomationManager
{
    Task RunAll();
    Task Run(string code);
    Task Run(string code, Dictionary<string, string> args);
}
