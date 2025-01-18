using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Options;
using Serilog;
using XCloud.Core.Metadata;
using XCloud.Helpers;
using XCloud.Sharing.Api;
using XCloud.Sharing.Api.Dto.Shares;
using XCloud.Sharing.Impl.Renderers;
using XCloud.Sharing.Settings;
using XCloud.Storage.Api;
using static XCloud.Helpers.Paths;
using static Microsoft.AspNetCore.WebUtilities.QueryHelpers;
using static XCloud.Helpers.FileNameHelpers;
using static System.String;
using static XCloud.Storage.Api.Paths;

namespace XCloud.Sharing.Impl;

public class ShareService(IOptions<ShareSettings> shareSettings,
    IStorage storage,
    Crypto crypto,
    MarkdownRenderer markdownRenderer,
    ExcalidrawRenderer excalidrawRenderer
) : IShareService
{
    private static readonly SlashString SharesDir = SysFolder / "share";
    private readonly ShareSettings _shareSettings = shareSettings.Value;

    public async Task<string?> Share(string path)
    {
        if (IsNullOrWhiteSpace(path)) return null;

        var absolutePath = await LookupPath(Uri.UnescapeDataString(path));
        if (absolutePath == null) return null;

        var sfi = await CreateSharedFileInfo(absolutePath, true);
        var existingSfi = await GetSharedFileInfo(sfi.Path);
        if (existingSfi != null)
        {
           sfi = existingSfi with { Links = sfi.Links };
        }
        await storage.PutJson(SharesDir / sfi.ShareKey, sfi);
        foreach (var link in sfi.Links)
        {
            var linkedSfi = await CreateSharedFileInfo(link.Path);
            await storage.PutJson(SharesDir / linkedSfi.ShareKey, linkedSfi);
        }

        return Path(_shareSettings.BasePublicUrl) / sfi.ShareKey;
    }

    private async Task<string?> LookupPath(string key)
    {
        var settings = await storage.LoadSettings();

        key = key.Trim();
        // obsidian://open?vault=memo&file=%E2%98%95%EF%B8%8F%20code%2FEnvsubst"
        if (key.StartsWith("obsidian://"))
        {
            var obsidianUrl = new Uri(key);
            var queryDictionary = ParseQuery(obsidianUrl.Query);
            var vault = queryDictionary["vault"].ToString();
            if (IsNullOrWhiteSpace(vault))
                throw new Exception("Vault not specified");
            if (!settings.Sharing.ObsidianVaults.TryGetValue(vault, out var vaultPath))
                throw new Exception($"Vault not found: {vault}");

            key = $"{vaultPath}/{queryDictionary["file"]}.md";
            Log.Information("Parsed Obsidian URL as object key {Key}", key);
        }

        if (await storage.Exists(key)) return key;

        return null;
    }

    private async Task<SharedFileInfo> CreateSharedFileInfo(string path, bool? share = null)
    {
        var storageItem = await storage.Get(path);
        if (storageItem == null) throw new Exception($"Path does not exist: {path}");
        var (frontmatter, bodyWithoutMeta) = Frontmatter.Parse(await storageItem.Content.ReadAllStringAsync());
        if (share != null)
        {
            frontmatter ??= new Frontmatter();
            frontmatter.Share ??= new();
            frontmatter.Share.shared = share.Value;
            var mdown = frontmatter.PrependYamlTag(bodyWithoutMeta);
            await storage.Put(path, mdown.ToStream());
        }
        var linkedShareKeys = Markdown.Parse(bodyWithoutMeta)
            .Descendants()
            .OfType<LinkInline>()
            .Where(l => !IsNullOrWhiteSpace(l.Url))
            // Only relative
            .Where(l => !Uri.TryCreate(l.Url, UriKind.Absolute, out _))
            .Select(l =>
            {
                var absolutePath = ResolveRelativePath(Uri.UnescapeDataString(l.Url!), path);
                var title = l.FirstChild?.ToString();
                return new LinkedFileInfo(
                    crypto.GetShareKey(absolutePath),
                    absolutePath,
                    title);
            }).ToArray();
        var shareKey = crypto.GetShareKey(path);
        var accessKey = IsNullOrWhiteSpace(frontmatter?.Share?.passkey)
            ? null
            : crypto.GetShareAccessToken(shareKey, frontmatter.Share.passkey);
        var shared = frontmatter?.Share?.shared ?? false;

        // To get updated timestamp
        var storageMetaItem = await storage.Stat(path)
            ?? throw new Exception("WTF ???");

        return new SharedFileInfo(
            path,
            shareKey,
            linkedShareKeys,
            accessKey,
            shared,
            frontmatter?.Share?.index ?? false,
            frontmatter?.Title ?? Path(path).FileNameWithoutExtension,
            storageMetaItem.Checksum());
    }

    private async Task DeleteSharedFileInfo(string shareKey)
    {
        await storage.Rm(SharesDir / shareKey);
    }

    private async Task<SharedFileInfo?> GetSharedFileInfo(string shareKey)
    {
        var sfi = await storage.GetJson<SharedFileInfo>(SharesDir / shareKey);
        if (sfi == null) return null;
        var fi = await storage.Stat(sfi.Path);
        if (fi?.Checksum() == sfi.Checksum) return sfi;

        sfi = await CreateSharedFileInfo(sfi.Path);
        await storage.PutJson(SharesDir / shareKey, sfi);
        foreach (var link in sfi.Links)
        {
            var linkedSfi = await CreateSharedFileInfo(link.Path);
            await storage.PutJson(SharesDir / linkedSfi.ShareKey, linkedSfi);
        }

        return sfi;
    }

    private async Task<ShareEvaluation> ResolveSharePath(string[] shareKeyPath, string? accessKey)
    {
        if (shareKeyPath.Length == 0) return new();
        var settings = await storage.LoadSettings();
        var autoLinks = settings.Sharing.AutoSharedWhenLinked;

        SharedFileInfo? parent = null;
        SharedFileInfo? navRoot = null;
        foreach (var shareKeySegment in shareKeyPath)
        {
            var sfi = await GetSharedFileInfo(shareKeySegment);
            if (sfi == null) return new();

            if (parent == null)
            {
                if (!sfi.Shared) return new();
                if (sfi.AccessKey != null && sfi.AccessKey != accessKey) return new (null, sfi);
            }
            else
            {
                if (!parent.Shared) return new();
                if (!parent.Index)
                {
                    if (autoLinks.All(d => !sfi.Path.StartsWith(d))) return new();
                }
            }

            navRoot = parent;
            parent = sfi;
        }

        var nav = navRoot is { Links.Length: > 0, Index: true, Title: not null }
            ? new NavigationInfo(
                new NavigationLink(Path(_shareSettings.BasePublicUrl) / navRoot.ShareKey, navRoot.Title),
                navRoot.Links
                    .Where(x => !IsNullOrWhiteSpace(x.Title))
                    .Select(x => new NavigationLink(Path(_shareSettings.BasePublicUrl) / navRoot.ShareKey / x.ShareKey, x.Title!))
                    .ToArray())
            : null;

        return new (parent, null, nav);
    }

    public async Task<ShareBase?> GetShare(string[] shareKeyPath, long firstByte, long? lastByte, ShareType? asType, string? accessToken)
    {
        var shareKey = Path(shareKeyPath).ToString();
        var ev = await ResolveSharePath(shareKeyPath, accessToken);
        if (!ev.CanAccess())
        {
            if (!ev.IsBlocked()) return null;

            if (ev.BlockedBy.ShareKey != shareKey)
            {
                return new RedirectShare { Url = Path(_shareSettings.BasePublicUrl) / ev.BlockedBy.ShareKey };
            }

            return new RequestPasskeyShare { Key = ev.BlockedBy.ShareKey };
        }

        var path = ev.File.Path;

        Log.Information("Getting share {ShareKey} => {ResolvedPath} as {ShareType}, bytes {FirstByte}-{LastByte}",
            shareKeyPath, path, asType, firstByte, lastByte);

        var storageItem = await storage.Get(path, firstByte, lastByte);
        if (storageItem == null) return null;

        if (asType == ShareType.Raw) return new RawShare
        {
            Path = path,
            Key = shareKey,
            Content = storageItem.Content,
            TotalBytes = storageItem.ContentLength,
            ContentType = MimeType(path),
        };

        if (IsVideo(path))
        {
            await storageItem.Content.DisposeAsync();
            return new VideoShare
            {
                ContentUrl = $"{_shareSettings.BasePublicUrl}/{shareKey}?f=raw",
            };
        }

        if (IsExcalidraw(path))
        {
            return await excalidrawRenderer.RenderExcalidraw(shareKey, path, storageItem);
        }

        if (IsMarkdown(path))
        {
            var result = await markdownRenderer
                .RenderMarkdown(shareKey, path, storageItem, accessToken);
            if (result is MarkdownShare markdownShare)
            {
                markdownShare.Nav = ev.Navigation;
            }

            return result;
        }

        return new RawShare
        {
            Path = path,
            Key = shareKey,
            Content = storageItem.Content,
            ContentType = MimeType(path),
            TotalBytes = storageItem.ContentLength,
        };
    }

    public async Task UnShare(string key)
    {
        var sfi = await GetSharedFileInfo(key);
        if (sfi != null)
        {
            var storageItem = await storage.Get(sfi.Path)
                ?? throw new Exception($"Path does not exist: {sfi.Path}");
            var (frontmatter, bodyWithoutMeta) = Frontmatter.Parse(await storageItem.Content.ReadAllStringAsync());
            if (frontmatter != null)
            {
                frontmatter.Share = null;
                var mdown = frontmatter.PrependYamlTag(bodyWithoutMeta);
                await storage.Put(sfi.Path, mdown.ToStream());
            }

            await RmRecursive(sfi);
        }
    }

    private async Task RmRecursive(SharedFileInfo sfi)
    {
        foreach (var l in sfi.Links)
        {
            var child = await GetSharedFileInfo(l.ShareKey);
            if (child != null)
            {
                await RmRecursive(child);
            }
        }
        await DeleteSharedFileInfo(sfi.ShareKey);
    }

    public async Task<string?> GetShareAccessToken(string[] shareKeyPath, string passkey)
    {
        var sfi = await GetSharedFileInfo(shareKeyPath.Last());
        if (sfi == null) return null;

        var token = crypto.GetShareAccessToken(sfi.ShareKey, passkey);
        return sfi.AccessKey != token ? null : token;
    }
}
