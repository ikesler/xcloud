namespace XCloud.Common.Api;

public interface ITemplater
{
    Task<string?> Render(string? baseDir, string templateName, object model);
}
