using XCloud.Sharing.Api.Dto.Shares;

namespace XCloud.Sharing.Api;

public interface IShareService
{
    Task<string?> Share(string path);
    Task<ShareBase?> GetShare(string[] shareKeyPath, long firstByte, long? lastByte, ShareType? asType, string? accessToken);
    Task<string?> GetShareAccessToken(string[] shareKeyPath, string passkey);
    Task UnShare(string key);
}
