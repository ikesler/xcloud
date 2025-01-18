using XCloud.Core.Settings;
using XCloud.Ext.Storage;

namespace XCloud.Storage.Api;

public interface IStorage: IStorageProvider
{
    Task<XCloudSettings> LoadSettings();
    Task<DateTime> LocalTime();
    Task<ILockHandle?> Lock(string key, TimeSpan? expiresIn = null);
    Task<T?> GetJson<T>(string key);
    Task PutJson(string key, object value);
    Task<string?> KvGet(string key);
    Task KvSet(string key, string value);
}
