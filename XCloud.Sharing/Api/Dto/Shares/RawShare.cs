namespace XCloud.Sharing.Api.Dto.Shares;

public class RawShare: ShareBase
{
    public required Stream Content { get; init; }
    public required string ContentType { get; init; }
    public long FirstByte { get; set; }
    public long LastByte { get; set; }
    public long? TotalBytes { get; set; }
}
