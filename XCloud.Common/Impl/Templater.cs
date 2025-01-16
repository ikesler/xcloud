using Scriban;
using XCloud.Common.Api;
using XCloud.Storage.Api;

namespace XCloud.Common.Impl;

public class Templater(IStorage storage): ITemplater
{
    public async Task<string?> Render(string? baseDir, string templateName, object model)
    {
        if (string.IsNullOrWhiteSpace(baseDir)) return null;

        var templatePath = Path.Combine(baseDir, templateName);
        if (!await storage.Exists(templatePath)) return null;

        var template = await storage.Get(templatePath, 0, null);
        if (template == null) return null;

        using var templateReader = new StreamReader(template.Content);
        return await Template
            .Parse(await templateReader.ReadToEndAsync())
            .RenderAsync(model);
    }
}
