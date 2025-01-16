namespace XCloud.Storage.Api;

public interface IStorage
{
    Task<bool> Exists(string key);
    Task Rm(string key);
    Task<StorageMetaItem[]> Ls(string keyPrefix);
    Task<StorageMetaItem?> Stat(string key);
    Task<StorageItem?> Get(string key, long firstByte = 0, long? lastByte = null);
    Task Put(string key, Stream content);
    Dictionary<string, object> Cache { get; }
}
