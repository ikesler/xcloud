using System.Text.Encodings.Web;
using System.Text.Json;
using Serilog;
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

    public Task Rm(string key) => provider.Rm(key);

    public Task<StorageMetaItem[]> Ls(string keyPrefix) => provider.Ls(keyPrefix);

    public Task<StorageMetaItem?> Stat(string key) => provider.Stat(key);

    public Task<StorageItem?> Get(string key, long firstByte = 0, long? lastByte = null)
    {
        return provider.Get(key, firstByte, lastByte);
    }

    public Task Put(string key, Stream content) => provider.Put(key, content);

    public async Task<XCloudSettings> LoadSettings()
    {
        if (_cache.TryGetValue("Settings", out var settings)) return (XCloudSettings) settings;

        var newSettingsFile = await Get(SettingsFile)
            ?? throw new Exception("Could not load new settings file from storage");

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
        var result = JsonSerializer.Deserialize<T>(item.Content, JsonSerializerOptions);
        await item.Content.DisposeAsync();
        return result;
    }

    public async Task<string?> KvGet(string key)
    {
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

    public async Task<ILockHandle?> Lock(string key, TimeSpan? expiresIn = null)
    {
        var token = await GetLock(key);
        if (token == null) return await PutLock(key, expiresIn);
        if (token.ExpiresAt < DateTime.UtcNow)
        {
            return await PutLock(key, expiresIn);
        }
        Log.Warning("Could not acquire {Lock} lock. A token exists and not expired: {Token}", key, token);
        return null;
    }

    private async Task<LockToken?> GetLock(string key)
    {
        var lockPath = LockFolder / key;

        var lockItem = await Get(lockPath);
        if (lockItem != null)
        {
            return JsonSerializer.Deserialize<LockToken>(await lockItem.Content.ReadAllStringAsync())
                ?? throw new Exception("Could not deserialize lock token");
        }

        return null;
    }

    private async Task<LockHandle> PutLock(string key, TimeSpan? expiresIn)
    {
        var lockPath = LockFolder / key;

        var token = new LockToken(Guid.NewGuid().ToString(), expiresIn.HasValue ? DateTime.UtcNow + expiresIn : null);
        await Put(lockPath, JsonSerializer.Serialize(token).ToStream());

        return new LockHandle(async span =>
        {
            var actualToken = await GetLock(key);
            if (actualToken != token) throw new Exception("Lock has been lost");
            token = actualToken with
            {
                ExpiresAt = DateTime.UtcNow + span
            };
            await Put(lockPath, JsonSerializer.Serialize(token).ToStream());
        }, async () =>
        {
            var actualToken = await GetLock(key);
            if (actualToken != token) throw new Exception("Lock has been lost");
            await Rm(lockPath);
        });
    }
}
