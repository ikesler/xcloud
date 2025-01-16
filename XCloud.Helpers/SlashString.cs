namespace XCloud.Helpers;

public readonly struct SlashString(string path)
{
    private readonly string _path = path.Trim('/');

    public static SlashString operator /(SlashString left, SlashString right)
    {
        return new SlashString($"{left}/{right}");
    }

    public static SlashString operator /(SlashString left, string right)
    {
        return new SlashString($"{left}/{right.Trim('/')}");
    }

    public static implicit operator string(SlashString slashString)
    {
        return slashString._path;
    }

    public override string ToString() => _path;

    public string Extension => Path.GetExtension(_path) ?? "";
    public SlashString DirectoryName => new (Path.GetDirectoryName(_path) ?? "");
    public SlashString FileName => new (Path.GetFileName(_path) ?? "");
    public SlashString FileNameWithoutExtension => new (Path.GetFileNameWithoutExtension(_path) ?? "");
}
