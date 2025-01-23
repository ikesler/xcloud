using Microsoft.Extensions.Options;
using XCloud.Helpers;
using XCloud.Sharing.Settings;

namespace XCloud.Sharing.Impl;

public class Crypto(IOptions<ShareSettings> shareSettings)
{
    private const int ShareKeyLength = 20;

    private readonly ShareSettings _shareSettings = shareSettings.Value;

    public string GetShareKey(string path) => HashAndEncode($"{path}::{_shareSettings.ShareKeyPepper}");

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

    public static bool ValidateShareKey(string? shareKey)
    {
        return !string.IsNullOrWhiteSpace(shareKey)
            && shareKey.IsValidBase64Url()
            && shareKey.Length == ShareKeyLength;
    }

    private static string HashAndEncode(string value)
    {
        var hashBytes = System.Security.Cryptography.SHA256.HashData(value.ToBytes());
        var base68Bytes = System.Buffers.Text.Base64Url.EncodeToString(hashBytes);
        return base68Bytes[..ShareKeyLength];
    }
}
