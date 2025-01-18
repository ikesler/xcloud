using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Serilog;
using XCloud.Automations.Settings;
using XCloud.Core.Metadata;
using XCloud.Ext.Automation;
using XCloud.Ext.Storage;
using XCloud.Helpers;
using XCloud.Storage.Api;

namespace XCloud.Automations.Impl.AiNoteTitle;

public class AiTitleAutomation(IStorage storage,
    IOptions<AiTitleAutomationSettings> options,
    IOptions<Dictionary<string, AiSettings>> aiOptions
    ): IAutomation
{
    public const string AutomationCode = "ai_title";
    private const string LastRunKey = $"automation.{AutomationCode}.last_run";

    private static readonly TimeSpan TimePerNote = TimeSpan.FromMinutes(20);

    private readonly ChatClient _client = new(
        options.Value.Model,
        new ApiKeyCredential(aiOptions.Value[options.Value.AiEndpoint].Key),
        new OpenAIClientOptions
        {
            Endpoint = new Uri(aiOptions.Value[options.Value.AiEndpoint].Endpoint),
            NetworkTimeout = TimePerNote,
            RetryPolicy = new ClientRetryPolicy(0)
        });

    public string Code => AutomationCode;

    public async Task Run(Dictionary<string, string> args)
    {
        // Directory to scan
        var directory = args["directory"];
        // Ignore any existing title and always call AI
        bool.TryParse(args.GetValueOrDefault("override"), out var overrideTitle);
        // Scan all files in the directory, not just the new ones
        bool.TryParse(args.GetValueOrDefault("fill_gaps"), out var fillGaps);
        // Process only the specified file
        var fileName = args.GetValueOrDefault("file");
        // Key in the KV storage to store the last run date
        var lastRunKey = $"{LastRunKey}:{directory}";

        var lockHandle = await storage.Lock(nameof(AiTitleAutomation), TimePerNote);
        if (lockHandle == null) return;
        await using (lockHandle)
        {
            if (fillGaps || !DateTime.TryParse(await storage.KvGet(lastRunKey), out var lastRunDate))
            {
                lastRunDate = DateTime.MinValue;
            }

            var notesAfter = DateTime.UtcNow.ToString("s");

            var filesQuery = (await storage.Ls(directory))
                .Where(x => x.Type == StorageMetaItemType.File);

            if (!overrideTitle)
            {
                filesQuery = filesQuery.Where(x => x.UpdatedAtUtc > lastRunDate);
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                filesQuery = filesQuery.Where(x => x.Name == fileName);
            }

            var files = filesQuery.OrderBy(x => x.Name).ToArray();

            Log.Information("Processing {FilesCnt} new files in '{Directory}' since {LastRunDate}",
                files.Length,
                directory,
                lastRunDate);
            foreach (var file in files)
            {
                await HandleFile(file, overrideTitle);
                await lockHandle.ExpireIn(TimePerNote);
            }

            await storage.KvSet(lastRunKey, notesAfter);
        }
    }

    private async Task HandleFile(StorageMetaItem file, bool overrideTitle)
    {
        try
        {
            Log.Information("Generating AI title for {File}", file.Key);
            await HandleFileUnsafe(file, overrideTitle);
            Log.Information("Successfully generated AI title for {File}", file.Key);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to generate AI title for {File}", file.Key);
        }
    }

    private async Task HandleFileUnsafe(StorageMetaItem file, bool overrideTitle)
    {
        var storageItem = await storage.Get(file.Key);
        if (storageItem == null) return;

        var content = await storageItem.Content.ReadAllStringAsync();
        var (frontmatter, contentWithoutFrontmatter) = Frontmatter.Parse(content);
        var hasContent = !string.IsNullOrWhiteSpace(contentWithoutFrontmatter);
        var processedBefore = frontmatter?.Automation?.ContainsKey(Code) ?? false;
        var hasTitle = !string.IsNullOrWhiteSpace(frontmatter?.Title);
        if (!overrideTitle && (hasTitle || processedBefore)) return;
        if (!hasContent) return;

        var completion = await _client
            .CompleteChatAsync([new UserChatMessage(contentWithoutFrontmatter)], new ChatCompletionOptions
            {
                MaxOutputTokenCount = 50 // Enforcing short responses
            });
        if (completion.Value.FinishReason == ChatFinishReason.Length)
        {
            Log.Error("AI reached token limit. File {File}", file.Key);
            return;
        }
        if (completion.Value.FinishReason == ChatFinishReason.ContentFilter)
        {
            Log.Error("AI faced content filter. File {File}", file.Key);
            return;
        }
        var title = completion.Value.Content
            .ElementAtOrDefault(0)?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(title) || title.Contains('\n'))
        {
            Log.Error("AI title for {File} is invalid: {Title}", file.Key, title);
            return;
        }
        title = new string(title
            .SkipWhile(c => !char.IsLetter(c))
            .Reverse()
            .SkipWhile(c => !char.IsLetter(c))
            .Reverse()
            .ToArray());
        if (string.IsNullOrWhiteSpace(title))
        {
            Log.Error("AI title for {File} is still invalid: {Title}", file.Key, title);
            return;
        }

        frontmatter ??= new Frontmatter();
        frontmatter.Title = completion.Value.Content[0].Text;
        frontmatter.Automation ??= new Dictionary<string, string>();
        frontmatter.Automation[Code] = (await storage.LocalTime()).ToString("s");
        content = frontmatter.PrependYamlTag(contentWithoutFrontmatter);
        await storage.Put(file.Key, content.ToStream());
    }
}
