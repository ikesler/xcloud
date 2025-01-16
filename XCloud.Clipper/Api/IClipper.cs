using XCloud.Clipper.Api.Dto;

namespace XCloud.Clipper.Api;

public interface IClipper
{
    Task Clip(ClipRequest clipRequest);
    Task ClipBookmark(ClipRequest clipRequest);
    Task ClipEpub(ClipRequest clipRequest);
}
