namespace XCloud.Ext.Automation;

public interface IAutomation
{
    string Code { get; }
    Task Run(Dictionary<string, string> args);
}
