using Microsoft.Extensions.Options;
using XCloud.Helpers;
using XCloud.Sharing.Settings;

namespace XCloud.Sharing.Impl;

public class Crypto(IOptions<ShareSettings> shareSettings)
{
    private readonly ShareSettings _shareSettings = shareSettings.Value;

    public string GetShareKey(string path) => $"{path}::{_shareSettings.ShareKeyPepper}".Sha256().Replace("-", "")[..7];

    public string GetShareAccessToken(string shareKey, string passkey) =>
        $"{shareKey}::{passkey}::{_shareSettings.ShareKeyPepper}".Sha256();

    public bool ValidateShareAccessToken(string shareKey, string expectedPasskey, string? actualAccessToken)
    {
        return string.Equals(
            actualAccessToken,
            GetShareAccessToken(shareKey, expectedPasskey),
            StringComparison.OrdinalIgnoreCase
        );
    }
}
