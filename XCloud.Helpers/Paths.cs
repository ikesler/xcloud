namespace XCloud.Helpers;

public static class Paths
{
    public static SlashString Path(string path) => new(path);
    public static SlashString Path(string[] path) => path.Aggregate(Path(""), (a, b) => a / b);

    public static string ResolveRelativePath(string relativePath, string basePath)
    {
        if (!relativePath.StartsWith(".."))
        {
            return Path(basePath).DirectoryName / relativePath;
        }

        // E.g.:
        // basePath = f1/f2/23.md
        // relativePath = ../_resources/lol.png
        // result = f1/_resources/lol.png

        var relativeParts = relativePath.Split('/');
        var firstAbsolutePart = -1;
        for (var i = 0; i < relativeParts.Length; ++i)
        {
            if (relativeParts[i] != "..")
            {
                firstAbsolutePart = i;
                break;
            }
        }

        var baseParts = basePath.Split('/');
        var takeBaseParts = baseParts.Length - firstAbsolutePart - 1;

        return string.Join('/', baseParts.Take(takeBaseParts).Union(relativeParts.Skip(firstAbsolutePart)));
    }
}
