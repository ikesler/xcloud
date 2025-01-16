using XCloud.Helpers;

namespace XCloud.Storage.Api;

public enum StorageMetaItemType
{
    File,
    Directory
}

public record StorageMetaItem(string Name, string Key, StorageMetaItemType Type, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, long? Size)
{
    public string Checksum() => $"{Name}::{Size}::{UpdatedAtUtc.Ticks}".Sha256();
}
