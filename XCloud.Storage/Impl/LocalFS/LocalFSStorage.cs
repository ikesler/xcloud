using Microsoft.Extensions.Options;
using XCloud.Storage.Api;
using XCloud.Storage.Settings;

namespace XCloud.Storage.Impl.LocalFS;

public class LocalFSStorage(IOptions<StorageSettings> storageSettings) : IStorage
{
    private readonly StorageSettings _storageSettings = storageSettings.Value;

    public async Task Rm(string key)
    {
        var path = Path.Combine(_storageSettings.LocalFSRoot, key);
        File.Delete(path);
    }

    public async Task<StorageMetaItem[]> Ls(string keyPrefix)
    {
        var path = Path.Combine(_storageSettings.LocalFSRoot, keyPrefix);
        var di = new DirectoryInfo(path);
        if (!di.Exists) return [];

        return di.EnumerateFileSystemInfos()
            .Select(x => new StorageMetaItem(
                x.Name,
                Path.Combine(keyPrefix, x.Name),
                x is FileInfo
                    ? StorageMetaItemType.File
                    : StorageMetaItemType.Directory,
                x.CreationTimeUtc,
                x.LastWriteTimeUtc,
                x is FileInfo fi
                    ? fi.Length
                    : null))
            .ToArray();
    }

    public async Task<StorageMetaItem?> Stat(string key)
    {
        var path = Path.Combine(_storageSettings.LocalFSRoot, key);
        if (Directory.Exists(path))
        {
            var di = new DirectoryInfo(path);
            return new StorageMetaItem(
                di.Name,
                path,
                StorageMetaItemType.Directory,
                di.CreationTimeUtc,
                di.LastWriteTimeUtc,
                null);
        }
        var fi = new FileInfo(path);
        return new StorageMetaItem(
            fi.Name,
            path,
            StorageMetaItemType.File,
            fi.CreationTimeUtc,
            fi.LastWriteTimeUtc,
            fi.Length);
    }

    public async Task<bool> Exists(string key)
    {
        var path = Path.Combine(_storageSettings.LocalFSRoot, key);
        return File.Exists(path);
    }

    public async Task<StorageItem?> Get(string key, long firstByte, long? lastByte)
    {
        var path = Path.Combine(_storageSettings.LocalFSRoot, key);
        var fileName = Path.GetFileName(path);
        if (!File.Exists(path)) return null;

        var fi = new FileInfo(path);

        return new StorageItem(
            fileName,
            File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
            fi.Length,
            fi.LastWriteTimeUtc);
    }

    public async Task Put(string key, Stream content)
    {
        var path = Path.Combine(_storageSettings.LocalFSRoot, key);
        var directory = Path.GetDirectoryName(path);
        if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        await using var writeStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(writeStream);
    }

    public Dictionary<string, object> Cache { get; } = new();
}
