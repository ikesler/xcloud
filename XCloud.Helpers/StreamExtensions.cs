using System;
using System.Text;

namespace XCloud.Helpers;

public static class StreamExtensions
{
    public static string ReadAllString(this Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static async Task<string> ReadAllStringAsync(this Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    public static byte[] ReadAllBytes(this Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static async Task CopyTo(this Stream source, Stream target, long firstByte, long? lastByte)
    {
        source.Seek(firstByte, SeekOrigin.Begin);
        var buffer = new byte[500_000];

        var bytesToCopy = lastByte - firstByte + 1;
        int readBytes;
        var copiedBytes = 0;
        do
        {
            var bytesToRead = bytesToCopy.HasValue
                ? Math.Min(bytesToCopy.Value - copiedBytes, buffer.Length)
                :  buffer.Length;
            readBytes = await source.ReadAsync(buffer.AsMemory(0, (int) bytesToRead));
            await target.WriteAsync(buffer.AsMemory(0, readBytes));
            await target.FlushAsync();
            copiedBytes += readBytes;
        } while (readBytes > 0 && copiedBytes < bytesToCopy);
    }
}
