using System.Text.Encodings.Web;
using System.Text.Json;
using Serilog;
using XCloud.Core.Settings;
using XCloud.Helpers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static XCloud.Storage.Api.Paths;

namespace XCloud.Storage.Api;

public static class StorageExtensions
{
    private static readonly SlashString LockFolder = SysFolder / "lock";
    private static readonly SlashString KvFile = SysFolder / "kv.json";
    private static readonly SlashString SettingsFile = SysFolder / "settings.yaml";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public static async Task<XCloudSettings> LoadSettings(this IStorage storage)
    {
        if (!storage.Cache.TryGetValue("Settings", out var settings))
        {
            var newSettingsFile = await storage.Get(SettingsFile)
                ?? throw new Exception("Could not load new settings file from storage");

            settings = YamlDeserializer
                .Deserialize<XCloudSettings>(await newSettingsFile.Content.ReadAllStringAsync());
            storage.Cache["Settings"] = settings;
        }

        return (XCloudSettings) settings;
    }

    public static async Task<DateTime> LocalTime(this IStorage storage)
    {
        var settings = await storage.LoadSettings();
        return string.IsNullOrWhiteSpace(settings.TimeZone)
            ? DateTime.Now
            : DateTime.UtcNow + TimeZoneInfo
                .FindSystemTimeZoneById(settings.TimeZone)
                .BaseUtcOffset;
    }

    public class LockHandle(Func<TimeSpan, ValueTask> expireIn, Func<ValueTask> dispose): IAsyncDisposable
    {
        public ValueTask ExpireIn(TimeSpan span) => expireIn(span);
        public ValueTask DisposeAsync() => dispose();
    }

    private record LockToken(string Id, DateTime? ExpiresAt);

    public static async Task<LockHandle?> Lock(this IStorage storage, string key, TimeSpan? expiresIn = null)
    {
        var token = await GetLock(storage, key);
        if (token == null) return await PutLock(storage, key, expiresIn);
        if (token.ExpiresAt < DateTime.UtcNow)
        {
            return await PutLock(storage, key, expiresIn);
        }
        Log.Warning("Could not acquire {Lock} lock. A token exists and not expired: {Token}", key, token);
        return null;
    }

    public static async Task<string?> KvGet(this IStorage storage, string key)
    {
        var kvItem = await storage.Get(KvFile);
        if (kvItem != null)
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(await kvItem.Content.ReadAllStringAsync())
                ?.GetValueOrDefault(key);
        }

        return null;
    }

    public static async Task KvSet(this IStorage storage, string key, string value)
    {
        var kvItem = await storage.Get(KvFile);
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

        await storage.Put(KvFile, JsonSerializer.Serialize(kv).ToStream());
    }

    public static async Task PutJson(this IStorage storage, string key, object value)
    {
        await storage.Put(key, JsonSerializer.Serialize(value, JsonSerializerOptions).ToStream());
    }

    public static async Task<T?> GetJson<T>(this IStorage storage, string key)
    {
        var item = await storage.Get(key);
        if (item == null) return default;
        var result = JsonSerializer.Deserialize<T>(item.Content, JsonSerializerOptions);
        await item.Content.DisposeAsync();
        return result;
    }

    private static async Task<LockToken?> GetLock(IStorage storage, string key)
    {
        var lockPath = LockFolder / key;

        var lockItem = await storage.Get(lockPath);
        if (lockItem != null)
        {
            return JsonSerializer.Deserialize<LockToken>(lockItem.Content.ReadAllString())
                ?? throw new Exception("Could not deserialize lock token");
        }

        return null;
    }

    private static async Task<LockHandle> PutLock(IStorage storage, string key, TimeSpan? expiresIn)
    {
        var lockPath = LockFolder / key;

        var token = new LockToken(Guid.NewGuid().ToString(), expiresIn.HasValue ? DateTime.UtcNow + expiresIn : null);
        await storage.Put(lockPath, JsonSerializer.Serialize(token).ToStream());

        return new LockHandle(async span =>
        {
            var actualToken = await GetLock(storage, key);
            if (actualToken != token) throw new Exception("Lock has been lost");
            token = actualToken with
            {
                ExpiresAt = DateTime.UtcNow + span
            };
            await storage.Put(lockPath, JsonSerializer.Serialize(token).ToStream());
        }, async () =>
        {
            var actualToken = await GetLock(storage, key);
            if (actualToken != token) throw new Exception("Lock has been lost");
            await storage.Rm(lockPath);
        });
    }
}
