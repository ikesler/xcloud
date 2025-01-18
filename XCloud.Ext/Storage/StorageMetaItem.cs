namespace XCloud.Ext.Storage;

public enum StorageMetaItemType
{
    File,
    Directory
}

public record StorageMetaItem(string Name,
    string Key,
    StorageMetaItemType Type,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    long? Size);
