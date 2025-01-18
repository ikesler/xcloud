namespace XCloud.Sharing.Settings;

public class ShareSettings
{
    public required string BasePublicUrl { get; init; }
    public required string ShareKeyPepper { get; init; }
    public required string ExcalidrawEmbeddingBasePath { get; init; }
    public int ExcalidrawEmbeddingQuotaBytes { get; set; }
}
