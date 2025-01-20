using System.Text.Encodings.Web;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable StringLiteralTypo

namespace XCloud.Core.Metadata;

public class Frontmatter
{
    private const string TripleDash = "---";
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();
    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .Build();

    public OgMetadata? Og { get; set; }
    public string? PreviewImage { get; set; }
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public ShareOptions? Share { get; set; }

    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; set; }

    public long? ReaderaTimestamp { get; set; }
    public string? ReaderaUri { get; set; }

    public Dictionary<string, string>? Automation { get; set; }
    public object? Tags { get; init; }

    [YamlMember(Alias = "excalidraw-plugin")]
    public string? ExcalidrawPlugin { get; set; }

    public static (Frontmatter?, string) Parse(string note)
    {
        try
        {
            return ParseInternal(note);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred during parsing note metadata");
            throw;
        }
    }

    private static (Frontmatter?, string) ParseInternal(string note)
    {
        var metaStringStart = note.IndexOf(TripleDash, StringComparison.InvariantCulture);
        var metaStringEnd = note.IndexOf(TripleDash, metaStringStart + 1, StringComparison.InvariantCulture);
        if (metaStringStart != -1 && metaStringEnd != -1 && metaStringEnd > metaStringStart)
        {
            var metaString = note.Substring(
                metaStringStart + TripleDash.Length,
                metaStringEnd - metaStringStart - TripleDash.Length);
            Log.Information("Parsed metadata: {Metadata}", metaString);

            var clipMetadata = YamlDeserializer.Deserialize<Frontmatter>(metaString);
            var noteWithoutMetaTag = note.Remove(metaStringStart, TripleDash.Length + metaString.Length + TripleDash.Length);

            return (clipMetadata, noteWithoutMetaTag);
        }

        return (null, note);
    }

    private string ToYamlTag()
    {
        var yaml = YamlSerializer.Serialize(this);
        return $"{TripleDash}\r\n{yaml}\r\n{TripleDash}\r\n";
    }

    public string PrependYamlTag(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return ToYamlTag();
        return ToYamlTag() + content.Trim();
    }

    public string ToOgMetaTags()
    {
        if (Og == null) return "";

        var metaDic = new Dictionary<string, string?>
        {
            ["og:type"] = Og.type,
            ["og:title"] = Og.title,
            ["og:url"] = Og.url,
            ["og:image"] = Og.image?.url ?? PreviewImage,
            ["og:image:width"] = Og.image?.width?.ToString(),
            ["og:image:height"] = Og.image?.height?.ToString(),
            ["og:description"] = Og.description,
        };

        return string.Concat(metaDic.Keys.Where(k => metaDic[k] != null).Select(k =>
            $"<meta property=\"{HtmlEncoder.Default.Encode(k)}\" content=\"{HtmlEncoder.Default.Encode(metaDic[k]!)}\">"));
    }
}
