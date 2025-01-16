namespace XCloud.Core.Metadata;

public class ShareOptions
{
    public string? title { get; set; }
    /// <summary>
    /// If the share is a markdown note and its frontmatter has "url" field - redirect to that URL without returning the content.
    /// </summary>
    public bool redirect { get; set; }
    public string? passkey { get; set; }
    public bool shared { get; set; }
    
    /// <summary>
    /// Child shares should show navigation between siblings in the parent.
    /// </summary>
    public bool index { get; set; }
}
