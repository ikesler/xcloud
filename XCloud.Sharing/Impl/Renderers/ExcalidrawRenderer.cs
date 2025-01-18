using Microsoft.Extensions.Options;
using Serilog;
using XCloud.Ext.Storage;
using XCloud.Helpers;
using XCloud.Sharing.Api.Dto.Shares;
using XCloud.Sharing.Settings;
using XCloud.Storage.Api;
using static XCloud.Helpers.Paths;
using static XCloud.Helpers.FileNameHelpers;

namespace XCloud.Sharing.Impl.Renderers;

public class ExcalidrawRenderer(IOptions<ShareSettings> shareSettings, IStorage storage)
{
    private readonly ShareSettings _shareSettings = shareSettings.Value;

    public async Task<ShareBase> RenderExcalidraw(string shareKey, string path, StorageItem storageItem)
    {
        var markdown = await storageItem.Content.ReadAllStringAsync();

        var compressedJson = markdown.SubstringBetween("```compressed-json", "```");
        var embeddedFilesStrings = markdown.SubstringBetween("## Embedded Files", "%%")
            .Split('\n')
            .Select(x => x.Split(':').Select(y => y.Trim()).ToArray())
            .Where(x => x.Length == 2)
            .Select(pair => (
                fileKey: pair[0],
                fileName: pair[1].SubstringBetween("[[", "]]")
            ))
            .ToArray();

        long loadedEmbeddingsBytes = 0;
        var embeddedFiles = new Dictionary<string, EmbeddedFile>();
        foreach (var (fileKey, fileName) in embeddedFilesStrings)
        {
            var fileStorageItem = await storage.Get(Path(_shareSettings.ExcalidrawEmbeddingBasePath) / fileName);
            if (fileStorageItem == null)
            {
                Log.Warning("File {FileName} embedded into Excalidraw share {ExcalidrawPath} not found", fileName,
                    path);
                continue;
            }

            loadedEmbeddingsBytes += fileStorageItem.ContentLength;
            if (loadedEmbeddingsBytes > _shareSettings.ExcalidrawEmbeddingQuotaBytes)
            {
                Log.Warning("Excalidraw embedding quota of {ExcalidrawEmbeddingQuotaBytes} has exceeded",
                    _shareSettings.ExcalidrawEmbeddingQuotaBytes);
                break;
            }

            var mimeType = MimeType(fileStorageItem.FileName);
            var fileBytes = fileStorageItem.Content.ReadAllBytes();
            var fileBase64 = Convert.ToBase64String(fileBytes);
            embeddedFiles[fileKey] = new EmbeddedFile(mimeType, fileBase64);
        }

        return new ExcalidrawShare
        {
            Title = Path(path).FileName.ToString().Replace(".excalidraw.md", ""),
            Path = path,
            Key = shareKey,
            CompressedDrawingData = compressedJson,
            EmbeddedFiles = embeddedFiles,
        };
    }
}
