using XCloud.Clipper.Api.Dto;
using XCloud.Core.Settings;

namespace XCloud.Clipper.Impl;

internal class ClipContext
{
    public string? OriginalHtml { get; set; }
    public required ClipRequest ClipRequest { get; init; }
    public required ClipperSettings ClipperSettings { get; init; }
    public Dictionary<string, string> OgMetaTags { get; set; } = new();
    public required HttpClient HttpClient { get; init; }
}
