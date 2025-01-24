namespace XCloud.Sharing.Api.Dto.Shares;

public class RequestPasskeyShare : ShareBase
{
    public required string? Hint { get; init; }
}
