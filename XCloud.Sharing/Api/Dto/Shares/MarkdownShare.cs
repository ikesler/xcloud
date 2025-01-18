
using XCloud.Sharing.Impl;

namespace XCloud.Sharing.Api.Dto.Shares;

public class MarkdownShare: ShareBase
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? OgMeta { get; init; }
    public NavigationInfo? Nav { get; set; }
}
