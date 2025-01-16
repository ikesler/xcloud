namespace XCloud.Automations.Api;

public interface IAutomation
{
    string Code { get; }
    Task Run(Dictionary<string, string> args);
}
