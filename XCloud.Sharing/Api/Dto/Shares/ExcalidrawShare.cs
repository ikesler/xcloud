namespace XCloud.Sharing.Api.Dto.Shares;

public class ExcalidrawShare: ShareBase
{
    public required string Title { get; init; }
    public required string CompressedDrawingData { get; init; }
    public required Dictionary<string, EmbeddedFile> EmbeddedFiles { get; init; }
}

public record EmbeddedFile(string MimeType, string Base64Content);
