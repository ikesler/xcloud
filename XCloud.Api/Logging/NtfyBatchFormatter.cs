using System.Text.Json;
using Serilog.Sinks.Http;

namespace XCloud.Api.Logging;

public class NtfyBatchFormatter: IBatchFormatter
{
    private readonly string _topic;

    public NtfyBatchFormatter(string topic)
    {
        _topic = topic;
    }
    public void Format(IEnumerable<string> logEvents, TextWriter output)
    {
        var entry = new
        {
            topic = _topic,
            message = string.Join("\n", logEvents.Select(x => $"\u2757 {x}")),
            title = "XCloud Error(s)",
            tags = new[] { "warning" },
        };
        output.WriteLine(JsonSerializer.Serialize(entry));
    }
}
