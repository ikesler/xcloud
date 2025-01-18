namespace XCloud.Storage.Impl;

internal record LockToken(string Id, DateTime? ExpiresAt);
