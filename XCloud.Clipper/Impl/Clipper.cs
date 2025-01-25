using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using QuickEPUB;
using XCloud.Helpers;
using ReverseMarkdown;
using Serilog;
using SmartReader;
using XCloud.Clipper.Api;
using XCloud.Clipper.Api.Dto;
using XCloud.Common.Api;
using XCloud.Core.Metadata;
using XCloud.Core.Settings;
using XCloud.Storage.Api;
using static XCloud.Helpers.Paths;
using static System.String;
using static XCloud.Helpers.FileNameHelpers;

namespace XCloud.Clipper.Impl;

public class Clipper(IStorage storage, ITemplater templater) : IClipper
{
    public async Task Clip(ClipRequest clipRequest)
    {
        var ctx = await InitContext(clipRequest);
        var article = await GetArticle(ctx);
        var (path, fileName) = await GetStoragePath(Path(ctx.ClipperSettings.ClipBasePath), article.Title, ".md");

        var converter = new Converter(new Config
        {
            UnknownTags = Config.UnknownTagsOption.Bypass,
            RemoveComments = true,
            GithubFlavored = true,
        });
        var html = await PostProcess(article.Content, fileName, ctx);

        var mdown = converter.Convert(html);
        if (string.IsNullOrWhiteSpace(mdown) && !string.IsNullOrWhiteSpace(ctx.OriginalHtml))
        {
            mdown = converter.Convert(await PostProcess(ctx.OriginalHtml, fileName, ctx));
        }
        mdown = (await GetFrontmatter(ctx, article)).PrependYamlTag(mdown);

        await storage.Put(path, mdown.ToStream());
    }

    public async Task ClipBookmark(ClipRequest clipRequest)
    {
        var ctx = await InitContext(clipRequest);
        var article = await GetArticle(ctx);
        var (path, fileName) = await GetStoragePath(Path(ctx.ClipperSettings.BookmarkBasePath), article.Title, ".md");

        var mdown = await templater.Render(ctx.ClipperSettings.TemplateDirectory,
            "xcloud_bookmark.liquid",
            new
            {
                article.Title,
                clipRequest.Url,
                Image = await SaveResource(fileName, article.FeaturedImage, ctx),
                article.Excerpt,
                article.Byline,
            }) ?? clipRequest.Url.ToString();
        mdown = (await GetFrontmatter(ctx, article)).PrependYamlTag(mdown);

        await storage.Put(path, mdown.ToStream());
    }

    public async Task ClipEpub(ClipRequest clipRequest)
    {
        var ctx = await InitContext(clipRequest);
        var article = await GetArticle(ctx);
        var (path, _) = await GetStoragePath(Path(ctx.ClipperSettings.EpubBasePath), article.Title, ".epub");
        
        var doc = new Epub(article.Title, article.Author ?? "Unknown Author");
        var infoSection = await templater.Render(ctx.ClipperSettings.TemplateDirectory,
            "xcloud_epub_info.liquid",
            article);
        if (infoSection != null)
        {
            doc.AddSection("Xcloud Clipper", infoSection);
        }
        var contentSection = await templater.Render(ctx.ClipperSettings.TemplateDirectory,
            "xcloud_epub_content.liquid",
            article);
        doc.AddSection(article.Title, contentSection ?? article.Content);

        using var ms = new MemoryStream();
        doc.Export(ms);
        await storage.Put(path, new MemoryStream(ms.ToArray()));
    }

    private async Task<ClipContext> InitContext(ClipRequest clipRequest)
    {
        var ctx = new ClipContext
        {
            ClipRequest = clipRequest,
            ClipperSettings = (await storage.LoadSettings()).Clipper,
            HttpClient = new HttpClient(),
        };

        foreach (var (key, value) in ctx.ClipperSettings.GlobalHeadersToAdd)
        {
            ctx.HttpClient.DefaultRequestHeaders.Add(key, value);
        }
        var host = clipRequest.Url.Host.ToLowerInvariant();
        if (host.StartsWith("www.")) host = host.Replace("www.", "");
        if (ctx.ClipperSettings.DomainHeadersToAdd.TryGetValue(host, out var headers))
        {
            foreach (var (key, value) in headers)
            {
                ctx.HttpClient.DefaultRequestHeaders.Add(key, value);
            }
        }

        return ctx;
    }

    private static async Task<Article> GetArticle(ClipContext ctx)
    {
        var reader = new SmartReader.Reader(ctx.ClipRequest.Url.ToString(),
            await ctx.HttpClient.GetStreamAsync(ctx.ClipRequest.Url));
        reader.AddCustomOperationStart(e => PreProcess(e, ctx.ClipperSettings, ctx.ClipRequest.Url.Host, ctx));
        return await reader.GetArticleAsync();
    }

    private async Task<(string path, string fileName)> GetStoragePath(SlashString basePath, string? title, string extension)
    {
        var fileName = (IsNullOrWhiteSpace(title) ? Guid.NewGuid().ToString() : title).CreateFileName();

        string nameWithExtension;
        string fullPath;
        var suffixGen = new FileNameSuffixGenerator();
        do
        {
            nameWithExtension = $"{fileName}{suffixGen.Next()}{extension}";
            fullPath = basePath / nameWithExtension;
        } while (await storage.Exists(fullPath));

        return (fullPath, nameWithExtension);
    }

    private async Task<Frontmatter> GetFrontmatter(ClipContext ctx, Article article)
    {
        var now = await storage.LocalTime();
        var frontmatter = new Frontmatter
        {
            CreatedAt = now.ToString("s"),
            UpdatedAt = now.ToString("s")
        };
        frontmatter.Url = ctx.ClipRequest.Url.ToString();
        frontmatter.Og = new OgMetadata
        {
            image = ctx.OgMetaTags.ContainsKey("og:image")
                ? new OgImageMetadata
                {
                    url = ctx.OgMetaTags["og:image"],
                    width = ctx.OgMetaTags.GetValueOrDefault("og:image:width").ToNullDouble(),
                    height = ctx.OgMetaTags.GetValueOrDefault("og:image:height").ToNullDouble(),
                }
                : null,
            title = ctx.OgMetaTags.GetValueOrDefault("og:title"),
            description = ctx.OgMetaTags.GetValueOrDefault("og:description"),
            site_name = ctx.OgMetaTags.GetValueOrDefault("og:site_name"),
            url = ctx.OgMetaTags.GetValueOrDefault("og:url"),
            type = ctx.OgMetaTags.GetValueOrDefault("og:type"),
        };
        frontmatter.PreviewImage = ctx.OgMetaTags.GetValueOrDefault("og:image");
        frontmatter.Title = ctx.OgMetaTags.GetValueOrDefault("og:title") ?? article.Title;

        return frontmatter;
    }

    private static void PreProcess(IElement e, ClipperSettings settings, string host, ClipContext ctx)
    {
        host = host.ToLowerInvariant();
        if (host.StartsWith("www.")) host = host.Replace("www.", "");

        var selectorsToRemove = settings.GlobalSelectorsToRemove.ToList();
        if (settings.DomainSelectorsToRemove.TryGetValue(host, out var value))
        {
            selectorsToRemove.AddRange(value);
        }

        foreach (var selector in selectorsToRemove)
        {
            var itemsToRemove = e.QuerySelectorAll(selector);
            foreach (var itemToRemove in itemsToRemove)
            {
                itemToRemove.Remove();
            }
        }

        var body = e.QuerySelector("body");
        if (settings.DomainSelectorsToInclude.TryGetValue(host, out var includeSelector))
        {
            var includeElement = e.QuerySelector(includeSelector);
            if (includeElement != null && body != null)
            {
                body.InnerHtml = includeElement.OuterHtml;
            }
            else
            {
                Log.Error("Include selector or body not found: {Selector}, {Body}", includeSelector, body);
            }
        }

        var imageLinks = e.QuerySelectorAll("a[href$='.jpg']");
        foreach (var imageLink in imageLinks)
        {
            if (imageLink.Children.Length == 1 && imageLink.Children.Single().TagName.Equals("img", StringComparison.InvariantCultureIgnoreCase))
            {
                // Obsidian fails to render markdown if an image has an indentation
                // This way we remove indentation in HTML and in consequent markdown
                imageLink.OuterHtml = imageLink.Children.Single().OuterHtml;
            }
        }

        ctx.OriginalHtml = body?.InnerHtml;
        ctx.OgMetaTags = e.QuerySelectorAll("meta[property^=og]")
            .DistinctBy(x => x.GetAttribute("property") ?? "")
            .ToDictionary(x => x.GetAttribute("property") ?? "", x => x.GetAttribute("content") ?? "");
    }

    private async Task<string> PostProcess(string content, string fileName, ClipContext ctx)
    {
        var config = Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);
        var document = (IHtmlDocument) await context.OpenAsync(req => req.Content(content));

        await SaveResources(document, fileName, ctx);

        return document.DocumentElement.OuterHtml;
    }

    private async Task SaveResources(IHtmlDocument document, string fileName, ClipContext ctx)
    {
        var images = document.QuerySelectorAll("img").OfType<IHtmlImageElement>();
        await Task.WhenAll(images.Select(async image =>
        {
            image.Source = await SaveResource(fileName, image.Source, ctx);
        }));
    }

    private async Task<string?> SaveResource(string fileName, string? uriStr, ClipContext ctx)
    {
        try
        {
            return await SaveResourceUnsafe(fileName, uriStr, ctx);
        }
        catch (Exception e)
        {
            Log.Warning(e, "An error occured while downloading resource {ResourceUrl}: {ErrorMessage}", uriStr, e.Message);
            return null;
        }
    }

    private async Task<string?> SaveResourceUnsafe(string fileName, string? uriStr, ClipContext ctx)
    {
        if (!Uri.TryCreate(uriStr, UriKind.Absolute, out _)) return null;

        var imageResponseMsg = await ctx.HttpClient.GetAsync(uriStr);

        var extension = MimeExtension(imageResponseMsg.Content.Headers.ContentType?.MediaType);
        var imagePath = Path(fileName).FileNameWithoutExtension / Guid.NewGuid() + extension;
        var imageStream = await imageResponseMsg.Content.ReadAsStreamAsync();
        await storage.Put(Path(ctx.ClipperSettings.ResourcesBasePath) / imagePath, imageStream);
        return Path(ctx.ClipperSettings.ResourcesRelativePath) / imagePath;
    }
}
