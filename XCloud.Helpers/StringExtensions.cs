using System.Text;

namespace XCloud.Helpers;

public static class StringExtensions
{
    private const int MaxFileNameLength = 100;

    private static readonly char[] InvalidPathCharacters = Path
        .GetInvalidFileNameChars()
        // These characters cause issues from various points of view: filesystem compatibility, Markdown links compatibility, OS-specific limitations, etc.
        .Union(['!', '?', '\\', '/', ':', ';', '*', '"', '<', '>', '|', '\'', ' ', '(', ')', '[', ']'])
        .ToArray();

    public static string CreateFileName(this string source)
    {
        source = source.Length > MaxFileNameLength ? source[..MaxFileNameLength] : source;
        var fileName = new StringBuilder(source);
        for (var i = 0; i < fileName.Length; ++i)
        {
            if (InvalidPathCharacters.Contains(fileName[i]))
            {
                fileName[i] = '_';
            }
        }

        return fileName
            .Replace("__", "_")
            .Replace("-_", "-")
            .Replace("_-", "-")
            .ToString()
            .Trim('-', '_', '.');
    }

    public static double? ToNullDouble(this string? src) => double.TryParse(src, out var result) ? result : null;

    public static string SubstringBetween(this string str, string start, string end)
    {
        var startIndex = str.IndexOf(start, StringComparison.Ordinal) + start.Length;
        var endIndex = str.IndexOf(end, startIndex, StringComparison.Ordinal) - 1;
        return str.Substring(startIndex, endIndex - startIndex + 1).Trim('\n', '\r', ' ', '\t');
    }

    /// <summary>
    /// Returns the index of the start of the contents in a StringBuilder
    /// </summary>
    /// <param name="value">The string to find</param>
    /// <param name="startIndex">The starting index.</param>
    /// <param name="ignoreCase">if set to <c>true</c> it will ignore case</param>
    /// <returns></returns>
    public static int IndexOf(this StringBuilder sb, string value, int startIndex = 0, bool ignoreCase = false)
    {            
        int index;
        int length = value.Length;
        int maxSearchLength = (sb.Length - length) + 1;

        if (ignoreCase)
        {
            for (int i = startIndex; i < maxSearchLength; ++i)
            {
                if (Char.ToLower(sb[i]) == Char.ToLower(value[0]))
                {
                    index = 1;
                    while ((index < length) && (Char.ToLower(sb[i + index]) == Char.ToLower(value[index])))
                        ++index;

                    if (index == length)
                        return i;
                }
            }

            return -1;
        }

        for (int i = startIndex; i < maxSearchLength; ++i)
        {
            if (sb[i] == value[0])
            {
                index = 1;
                while ((index < length) && (sb[i + index] == value[index]))
                    ++index;

                if (index == length)
                    return i;
            }
        }

        return -1;
    }

    public static Stream ToStream(this string value)
    {
        return new MemoryStream(value.ToBytes());
    }

    public static string Sha256(this string src) => BitConverter.ToString(
        System.Security.Cryptography.SHA256.HashData(src.ToBytes()));

    public static byte[] ToBytes(this string src) => Encoding.UTF8.GetBytes(src);

    public static string Base64(this string src) => Convert.ToBase64String(src.ToBytes());

    public static string Base64Url(this string src) => System.Buffers.Text.Base64Url
        .EncodeToString(src.ToBytes());

    public static bool IsValidBase64Url(this string str) => str.All(ch => char.IsAsciiLetter(ch)
        || char.IsAsciiDigit(ch)
        || ch == '-'
        || ch == '_');
}
