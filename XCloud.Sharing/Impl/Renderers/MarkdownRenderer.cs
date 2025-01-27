﻿using Markdig;
using Markdig.Extensions.MediaLinks;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Options;
using XCloud.Core.Metadata;
using XCloud.Ext.Storage;
using XCloud.Helpers;
using XCloud.Sharing.Api.Dto.Shares;
using XCloud.Sharing.Settings;
using static System.String;
using static XCloud.Helpers.Paths;

namespace XCloud.Sharing.Impl.Renderers;

public class MarkdownRenderer(Crypto crypto, IOptions<ShareSettings> shareSettings)
{
    private readonly ShareSettings _shareSettings = shareSettings.Value;

    public async Task<ShareBase> RenderMarkdown(string shareKey, string path, StorageItem storageItem, string? accessKey)
    {
        var body = await storageItem.Content.ReadAllStringAsync();
        var (frontmatter, bodyWithoutMeta) = Frontmatter.Parse(body);
        // By default, any note with url gets redirected
        // This is an expected behavior in case of clipped documents
        // Only if original is not available or the clipped version is significantly modified,
        // it makes sense to explicitly opt in to the clipped version
        if ((frontmatter?.Share?.Redirect ?? true) && !IsNullOrWhiteSpace(frontmatter?.Url))
        {
            return new RedirectShare { Key = shareKey, Path = path, Url = frontmatter.Url };
        }
        if (!IsNullOrWhiteSpace(frontmatter?.Share?.Passkey) && (
                IsNullOrWhiteSpace(accessKey) ||
                !crypto.ValidateShareAccessToken(shareKey, frontmatter.Share.Passkey, accessKey)
            )) {
            return new RequestPasskeyShare { Hint = frontmatter.Share.PasskeyHint };
        }

        var mdoc = Markdown.Parse(bodyWithoutMeta);

        RewriteLinks(shareKey, path, mdoc);

        await storageItem.Content.DisposeAsync();

        var markdigPipeline = new MarkdownPipelineBuilder().UseMediaLinks(new MediaOptions
        {
            ExtensionToMimeType =
            {
                [".mp4"] = "video/mp4",
            }
        }).Build();
        return new MarkdownShare
        {
            Path = path,
            Key = shareKey,
            Title = frontmatter?.Share?.Title ?? frontmatter?.Title ?? Path(path).FileNameWithoutExtension,
            Body = mdoc.ToHtml(markdigPipeline),
            OgMeta = frontmatter?.ToOgMetaTags(),
        };
    }

    private void RewriteLinks(string parentKey, string parentPath, MarkdownObject mdoc)
    {
        foreach (var link in mdoc.Descendants().OfType<LinkInline>())
        {
            if (IsNullOrWhiteSpace(link.Url)) continue;
            // Only relative
            if (Uri.TryCreate(link.Url, UriKind.Absolute, out _)) continue;

            var absolutePath = ResolveRelativePath(Uri.UnescapeDataString(link.Url), parentPath);
            if (IsNullOrWhiteSpace(absolutePath)) continue;
            var childShareKey = crypto.GetShareKey(absolutePath);
            var ext = Path(absolutePath).Extension;
            ext = ext == ".md" ? "" : "?f=raw";
            link.Url = Path(_shareSettings.BasePublicUrl) / parentKey / childShareKey + ext;
        }
    }
}
