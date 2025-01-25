namespace XCloud.Helpers;

public class FileNameSuffixGenerator
{
    private const int MaxNumIndex = 15;

    private int _index = -1;

    public string Next()
    {
        ++_index;
        if (_index == 0) return "";
        if (_index == MaxNumIndex) return Guid.NewGuid().ToString();
        if (_index > MaxNumIndex) throw new InternalBufferOverflowException("Too many file name generation attempts");

        return $"_{_index}";
    }
}
