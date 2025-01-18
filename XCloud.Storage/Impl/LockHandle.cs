using XCloud.Storage.Api;

namespace XCloud.Storage.Impl;

public class LockHandle(Func<TimeSpan, ValueTask> expireIn, Func<ValueTask> dispose): ILockHandle
{
    public ValueTask ExpireIn(TimeSpan span) => expireIn(span);
    public ValueTask DisposeAsync() => dispose();
}
