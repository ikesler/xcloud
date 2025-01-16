namespace XCloud.Core.Settings;

public class XCloudSettings
{
    public required string TimeZone { get; init; }
    public required SharingSettings Sharing { get; init; }
    public required ClipperSettings Clipper { get; init; }
    public required AutomationSettings[] Automations { get; init; }
}

public class SharingSettings
{
    public required Dictionary<string, string> ObsidianVaults { get; init; }
    public required string[] AutoSharedWhenLinked { get; init; }
}

public class AutomationSettings
{
    public required string Type { get; init; }
    public required Dictionary<string, string> Params { get; init; }
}
