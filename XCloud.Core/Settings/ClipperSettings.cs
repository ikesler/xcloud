namespace XCloud.Core.Settings;

public class ClipperSettings
{
    public required ReadEraSettings ReadEra { get; init; }
    public required string ClipBasePath { get; init; }
    public required string? TemplateDirectory { get; init; }
    public required string BookmarkBasePath { get; init; }
    public required string EpubBasePath { get; init; }
    public required string ResourcesBasePath { get; init; }
    public required string ResourcesRelativePath { get; init; }
    public required string[] GlobalSelectorsToRemove { get; init; }
    public required Dictionary<string, string[]> DomainSelectorsToRemove { get; set; }
    public required Dictionary<string, string> DomainSelectorsToInclude { get; set; }
    public required Dictionary<string, string> GlobalHeadersToAdd { get; set; }
    public required Dictionary<string, Dictionary<string, string>> DomainHeadersToAdd { get; set; }
}
