using XCloud.Helpers;

namespace XCloud.Storage.Api;

public record StorageItem(string FileName, Stream Content, long ContentLength, DateTime ModifiedAtUtc)
{
    public string Checksum() => $"{FileName}::{ContentLength}::{ModifiedAtUtc.Ticks}".Sha256();
}
