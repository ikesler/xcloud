using MimeMapping;

namespace XCloud.Helpers;

public static class FileNameHelpers
{
    public static bool IsExcalidraw(string name) =>
        name.EndsWith(".excalidraw.md", StringComparison.OrdinalIgnoreCase);

    public static bool IsMarkdown(string name) =>
        !IsExcalidraw(name) && name.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    public static bool IsVideo(string name) =>
        name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

    public static string MimeType(string name) => MimeUtility.GetMimeMapping(name);
}
