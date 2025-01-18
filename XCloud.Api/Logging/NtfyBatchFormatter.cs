using System.Text.Json;
using Serilog.Sinks.Http;

namespace XCloud.Api.Logging;

public class NtfyBatchFormatter(string topic) : IBatchFormatter
{
    public void Format(IEnumerable<string> logEvents, TextWriter output)
    {
        var entry = new
        {
            topic = topic,
            message = string.Join("\n", logEvents.Select(x => $"\u2757 {x}")),
            title = "XCloud Error(s)",
            tags = new[] { "warning" },
        };
        output.WriteLine(JsonSerializer.Serialize(entry));
    }
}
