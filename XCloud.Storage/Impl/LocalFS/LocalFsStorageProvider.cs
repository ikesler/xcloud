using Microsoft.Extensions.Options;
using XCloud.Ext.Storage;
using XCloud.Storage.Api;
using XCloud.Storage.Settings;

namespace XCloud.Storage.Impl.LocalFS;

public class LocalFsStorageProvider(IOptions<StorageSettings> storageSettings) : IStorageProvider
{
    private readonly StorageSettings _storageSettings = storageSettings.Value;

    public Task Rm(string key)
    {
        if (_storageSettings.ReadOnly) return Task.CompletedTask;

        var path = Path.Combine(_storageSettings.LocalFSRoot, key);
        File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<StorageMetaItem[]> Ls(string keyPrefix)
    {
        var path = Path.Combine(_storageSettings.LocalFSRoot, keyPrefix);
        var di = new DirectoryInfo(path);
        if (!di.Exists) return Task.FromResult<StorageMetaItem[]>([]);

        return Task.FromResult(di.EnumerateFileSystemInfos()
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
            .ToArray());
    }

    public Task<StorageMetaItem?> Stat(string key)
    {
        StorageMetaItem? result = null;
        var path = Path.Combine(_storageSettings.LocalFSRoot, key);
        var di = new DirectoryInfo(path);
        if (di.Exists)
        {
            result = new StorageMetaItem(
                di.Name,
                path,
                StorageMetaItemType.Directory,
                di.CreationTimeUtc,
                di.LastWriteTimeUtc,
                null);
        }
        var fi = new FileInfo(path);
        if (fi.Exists)
        {
            result = new StorageMetaItem(
                fi.Name,
                path,
                StorageMetaItemType.File,
                fi.CreationTimeUtc,
                fi.LastWriteTimeUtc,
                fi.Length);
        }

        return Task.FromResult(result);
    }

    public Task<bool> Exists(string key)
    {
        var path = Path.Combine(_storageSettings.LocalFSRoot, key);
        return Task.FromResult(File.Exists(path));
    }

    public Task<StorageItem?> Get(string key, long firstByte, long? lastByte)
    {
        var path = Path.Combine(_storageSettings.LocalFSRoot, key);
        var fileName = Path.GetFileName(path);

        StorageItem? result = null;
        var fi = new FileInfo(path);
        if (fi.Exists)
        {
            result = new StorageItem(
                fileName,
                File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                fi.Length,
                fi.LastWriteTimeUtc);
        }

        return Task.FromResult(result);
    }

    public async Task Put(string key, Stream content)
    {
        if (_storageSettings.ReadOnly) return;

        var path = Path.Combine(_storageSettings.LocalFSRoot, key);
        var directory = Path.GetDirectoryName(path);
        if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        await using var writeStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(writeStream);
    }

    public Dictionary<string, object> Cache { get; } = new();
}
