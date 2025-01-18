using XCloud.Ext.Storage;
using XCloud.Helpers;

namespace XCloud.Storage.Api;

public static class StorageExtensions
{
    public static string Checksum(this StorageItem item)
    {
        return $"{item.FileName}::{item.ContentLength}::{item.ModifiedAtUtc.Ticks}".Sha256();
    }

    public static string Checksum(this StorageMetaItem item)
    {
        return $"{item.Name}::{item.Size}::{item.UpdatedAtUtc.Ticks}".Sha256();
    }
}
