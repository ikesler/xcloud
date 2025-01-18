namespace XCloud.Storage.Settings;

public class StorageSettings
{
    public required string LocalFSRoot { get; init; }
    public bool ReadOnly { get; init; } = false;
}
