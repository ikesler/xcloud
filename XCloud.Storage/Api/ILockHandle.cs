namespace XCloud.Storage.Api;

public interface ILockHandle: IAsyncDisposable
{
    ValueTask ExpireIn(TimeSpan span);
}
