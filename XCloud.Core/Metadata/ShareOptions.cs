namespace XCloud.Core.Metadata;

public class ShareOptions
{
    public string? Title { get; set; }
    /// <summary>
    /// If the share is a markdown note and its frontmatter has "url" field - redirect to that URL without returning the content.
    /// </summary>
    public bool Redirect { get; set; }
    public string? Passkey { get; set; }
    public string? PasskeyHint { get; set; }
    public bool Shared { get; set; }
    
    /// <summary>
    /// Child shares should show navigation between siblings in the parent.
    /// </summary>
    public bool Index { get; set; }
}
