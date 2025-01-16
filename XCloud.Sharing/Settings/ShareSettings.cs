namespace XCloud.Sharing.Settings;

public class ShareSettings
{
    public string BasePublicUrl { get; set; } = null!;
    public string ShareKeyPepper { get; set; } = null!;
    public string ExcalidrawEmbeddingBasePath { get; set; } = null!;
    public int ExcalidrawEmbeddingQuotaBytes { get; set; }
}
