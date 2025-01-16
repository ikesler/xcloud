
using XCloud.Sharing.Impl;

namespace XCloud.Sharing.Api.Dto.Shares;

public class MarkdownShare: ShareBase
{
    public string Title { get; set; }
    public string Body { get; set; }
    public string? OgMeta { get; set; }
    public NavigationInfo? Nav { get; set; }
}
