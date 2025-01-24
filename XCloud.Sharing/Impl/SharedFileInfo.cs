namespace XCloud.Sharing.Impl;

public record SharedFileInfo(
    string Path,
    string ShareKey,
    LinkedFileInfo[] Links,
    string? AccessKey,
    bool Shared,
    bool Index,
    string? Title,
    string Checksum,
    string? PasskeyHint,
    DateTime? ExpiresAt);

public record LinkedFileInfo(string ShareKey, string Path, string? Title);
