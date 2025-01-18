namespace XCloud.Ext.Storage;

public record StorageItem(string FileName, Stream Content, long ContentLength, DateTime ModifiedAtUtc);
