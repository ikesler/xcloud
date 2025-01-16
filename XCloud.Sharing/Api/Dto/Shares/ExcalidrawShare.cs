namespace XCloud.Sharing.Api.Dto.Shares;

public class ExcalidrawShare: ShareBase
{
    public string Title { get; set; }
    public string CompressedDrawingData { get; set; }
    public Dictionary<string, EmbeddedFile> EmbeddedFiles { get; set; }
}

public record EmbeddedFile(string MimeType, string Base64Content);
