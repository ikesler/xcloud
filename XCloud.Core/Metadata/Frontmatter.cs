using System.Text.Encodings.Web;
using Serilog;
using YamlDotNet.Serialization;

namespace XCloud.Core.Metadata;

public class Frontmatter
{
    private const string TripleDash = "---";
    private static IDeserializer YamlDeserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
    private static ISerializer YamlSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .Build();

    public OgMetadata? og { get; set; }
    public string? preview_image { get; set; }
    public string? url { get; set; }
    public string? title { get; set; }
    public string? author { get; set; }
    public ShareOptions? share { get; set; }

    public string created_at { get; set; }
    public string updated_at { get; set; }

    public long? readera_timestamp { get; set; }
    public string? readera_uri { get; set; }

    public Dictionary<string, string>? automation { get; set; }
    public object tags { get; set; }

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
        if (og == null) return "";

        var metaDic = new Dictionary<string, string?>
        {
            ["og:type"] = og.type,
            ["og:title"] = og.title,
            ["og:url"] = og.url,
            ["og:image"] = og.image?.url ?? preview_image,
            ["og:image:width"] = og.image?.width?.ToString(),
            ["og:image:height"] = og.image?.height?.ToString(),
            ["og:description"] = og.description,
        };

        return string.Concat(metaDic.Keys.Where(k => metaDic[k] != null).Select(k =>
            $"<meta property=\"{HtmlEncoder.Default.Encode(k)}\" content=\"{HtmlEncoder.Default.Encode(metaDic[k]!)}\">"));
    }
}
