using System.Text.Encodings.Web;
using System.Text.Json;
using DotNext.Threading;
using XCloud.Core;
using XCloud.Core.Settings;
using XCloud.Ext.Storage;
using XCloud.Helpers;
using XCloud.Storage.Api;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static XCloud.Storage.Api.Paths;

namespace XCloud.Storage.Impl;

public class StorageDecorator(IStorageProvider provider): IStorage
{
    private static readonly SlashString LockFolder = SysFolder / "lock";
    private static readonly SlashString KvFile = SysFolder / "kv.json";
    private static readonly SlashString SettingsFile = SysFolder / "settings.yaml";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private Dictionary<string, object> _cache { get; } = new();

    public Task<bool> Exists(string key) => provider.Exists(key);

    public async Task Rm(string key)
    {
        using var @lock = await Lock(key);
        await provider.Rm(key);
    }

    public Task<StorageMetaItem[]> Ls(string keyPrefix) => provider.Ls(keyPrefix);

    public async Task<StorageMetaItem?> Stat(string key)
    {
        using var @lock = await Lock(key);
        return await provider.Stat(key);
    }

    public async Task<StorageItem?> Get(string key, long firstByte = 0, long? lastByte = null)
    {
        using var @lock = await Lock(key);
        return await provider.Get(key, firstByte, lastByte);
    }

    public async Task Put(string key, Stream content)
    {
        using var @lock = await Lock(key);

        await provider.Put(key, content);
    }

    public async Task<XCloudSettings> LoadSettings()
    {
        if (_cache.TryGetValue("Settings", out var settings)) return (XCloudSettings) settings;

        var newSettingsFile = await Get(SettingsFile)
            ?? throw new XCloudException("Could not load new settings file from storage");

        settings = YamlDeserializer
            .Deserialize<XCloudSettings>(await newSettingsFile.Content.ReadAllStringAsync());
        _cache["Settings"] = settings;

        return (XCloudSettings) settings;
    }

    public async Task<DateTime> LocalTime()
    {
        var settings = await LoadSettings();
        return string.IsNullOrWhiteSpace(settings.TimeZone)
            ? DateTime.Now
            : DateTime.UtcNow + TimeZoneInfo
                .FindSystemTimeZoneById(settings.TimeZone)
                .BaseUtcOffset;
    }

    public async Task PutJson(string key, object value)
    {
        await Put(key, JsonSerializer.Serialize(value, JsonSerializerOptions).ToStream());
    }

    public async Task<T?> GetJson<T>(string key)
    {
        var item = await Get(key);
        if (item == null) return default;
        var result = await JsonSerializer.DeserializeAsync<T>(item.Content, JsonSerializerOptions);
        await item.Content.DisposeAsync();
        return result;
    }

    public async Task<string?> KvGet(string key)
    {
        using var @lock = await Lock(KvFile, "kv");

        var kvItem = await Get(KvFile);
        if (kvItem != null)
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(await kvItem.Content.ReadAllStringAsync())
                ?.GetValueOrDefault(key);
        }

        return null;
    }

    public async Task KvSet(string key, string value)
    {
        using var @lock = await Lock(KvFile, "kv");

        var kvItem = await Get(KvFile);
        Dictionary<string, string> kv;
        if (kvItem != null)
        {
            kv = JsonSerializer.Deserialize<Dictionary<string, string>>(await kvItem.Content.ReadAllStringAsync())
                 ?? new Dictionary<string, string>();
        }
        else
        {
            kv = new Dictionary<string, string>();
        }

        kv[key] = value;

        await Put(KvFile, JsonSerializer.Serialize(kv).ToStream());
    }

    public async Task<IDisposable> Lock(string key, string? prefix = null)
    {
        // TODO: does not support multiple instances of the app
        // Change to a distributed lock solution when it becomes relevant
        return await $"{prefix ?? "storage_key"}:{key}".AcquireLockAsync(TimeSpan.FromSeconds(10));
    }
}
