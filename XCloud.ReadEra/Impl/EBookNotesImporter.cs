using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNext.Threading;
using Polly;
using Polly.Registry;
using Scriban;
using Serilog;
using XCloud.Core.Metadata;
using XCloud.Core.Settings;
using XCloud.Helpers;
using XCloud.ReadEra.Constants;
using XCloud.ReadEra.Models;
using XCloud.Storage.Api;

namespace XCloud.ReadEra.Impl;

public class EBookNotesImporter(ResiliencePipelineProvider<string> pollyProvider,
    IStorage storage)
{
    private static readonly object _lock = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ResiliencePipeline _polly = pollyProvider.GetPipeline(PollyPipelines.ReadEraImportBookmarks);

    public async Task ImportFromReadEraBackup(Stream backupFileStream)
    {
        var settings = await storage.LoadSettings();
        var citTemplates = await LoadCitationTemplates(settings.Clipper.ReadEra);
        var wordTemplates = await LoadWordTemplates(settings.Clipper.ReadEra);

        var library = await ReadLibraryJson(backupFileStream);
        var wordsByDoc = library.Words
            .GroupBy(x => x.Data.DocUri)
            .ToDictionary(x => x.Key, x => x.ToArray());
        foreach (var doc in library.Docs)
        {
            var words = wordsByDoc.GetValueOrDefault(doc.Uri) ?? [];
            if (doc.Citations.Length > 0 || words.Length > 0)
            {
                await _polly.ExecuteAsync(async _ => await SaveToMarkdown(
                    settings.Clipper, citTemplates, wordTemplates, doc, words));
            }
        }
    }

    private async Task<Dictionary<string, string>> LoadCitationTemplates(ReadEraSettings readEraSettings)
    {
        var files = Enum
            .GetValues<ReadEraColor>()
            .SelectMany(mark =>
            {
                var main = ($"{mark}", path: Path.Combine(
                    readEraSettings.TemplatesDirectory,
                    $"cit_{mark}.liquid"));
                var alt = ($"{mark}_alt", path: Path.Combine(
                    readEraSettings.TemplatesDirectory,
                    $"cit_{mark}_alt.liquid"));
                return new[] { main, alt };
            });
        var result = new Dictionary<string, string>();
        foreach (var (mark, path) in files)
        {
            var storageItem = await storage.Get(path);
            if (storageItem != null)
            {
                result[mark] = storageItem.Content.ReadAllString();
            }
        }

        return result;
    }

    private async Task<Dictionary<string, string>> LoadWordTemplates(ReadEraSettings readEraSettings)
    {
        var dictionaries = Enum.GetValues<ReadEraDictionary>();
        var files = dictionaries
            .SelectMany(dic =>
            {
                var main = ($"{dic}", path: Path.Combine(
                    readEraSettings.TemplatesDirectory,
                    $"word_{dic}.liquid"));
                var alt = ($"{dic}_alt", path: Path.Combine(
                    readEraSettings.TemplatesDirectory,
                    $"word_{dic}_alt.liquid"));
                return new[] { main, alt };
            });
        var result = new Dictionary<string, string>();
        foreach (var (mark, path) in files)
        {
            var storageItem = await storage.Get(path);
            if (storageItem != null)
            {
                result[mark] = storageItem.Content.ReadAllString();
            }
        }

        return result;
    }

    private async Task SaveToMarkdown(
        ClipperSettings settings,
        Dictionary<string, string> citTemplates,
        Dictionary<string, string> wordTemplates,
        ReadEraDoc doc,
        ReadEraWord[] words)
    {
        using var @lock = await _lock.AcquireWriteLockAsync(TimeSpan.FromSeconds(10));

        var now = await storage.LocalTime();
        var docTitle = string.IsNullOrWhiteSpace(doc.Data.DocTitle) ? doc.Data.DocFileNameTitle : doc.Data.DocTitle;
        Log.Information("ReadEraImporter: processing document {Title}", docTitle);
        var mdFileName = Regex.Replace(
            docTitle,
            @"[^0-9\p{L}_\-\. ]",
            "_",
            RegexOptions.IgnoreCase)
            .Trim(' ', '_') + ".md";
        var mdFilePath = Path.Combine(
            settings.ReadEra.NotesDirectory,
            mdFileName);

        var storageItem = await storage.Get(mdFilePath);
        var (parsedFrontmatter, bodyWithoutMeta) = storageItem == null
            ? (null, "")
            : Frontmatter.Parse(await storageItem.Content.ReadAllStringAsync());
        var frontmatter = parsedFrontmatter ?? new Frontmatter
        {
            created_at = now.ToString("s"),
            updated_at = now.ToString("s"),
            title = doc.Data.DocTitle,
            author = doc.Data.DocAuthors,
            readera_timestamp = 0,
            readera_uri = doc.Uri
        };

        var timestamp = frontmatter.readera_timestamp ?? 0;

        var entries = doc.Citations
            .Where(x => x.NoteModifiedTime > timestamp)
            .Select(x => new ImportEntry(x.NoteModifiedTime, x))
            .Union(words
                .Where(x => x.Data.WordModifiedTime > timestamp)
                .Select(x => new ImportEntry(x.Data.WordModifiedTime, x)))
            .OrderBy(x => x.Timestamp)
            .ToArray();

        var citCnt = entries.Count(x => x.ReadEraObject is ReadEraCitation);
        var wordCnt = entries.Count(x => x.ReadEraObject is ReadEraWord);

        Log.Information("ReadEraImporter:" +
                        " found {NumOfCitations} new citations" +
                        " and {NumOfWords} new words.", citCnt, wordCnt);

        if (entries.Length == 0) return;

        var mdoc = new StringBuilder(bodyWithoutMeta);

        foreach (var (_, obj) in entries)
        {
            if (obj is ReadEraCitation readEraCitation)
            {
                await ImportNote(readEraCitation, citTemplates, bodyWithoutMeta, mdoc);
            }

            if (obj is ReadEraWord readEraWord)
            {
                await ImportWord(readEraWord, wordTemplates, bodyWithoutMeta, mdoc);
            }
        }

        frontmatter.readera_timestamp = entries.Last().Timestamp;
        frontmatter.updated_at = now.ToString("s");

        await storage.Put(mdFilePath, frontmatter.PrependYamlTag(mdoc.ToString()).ToStream());
        Log.Information("ReadEraImporter: saved {MarkdownDocument} document. Timestamp changed from {OldTimestamp} to {NewTimestamp}",
            mdFilePath, timestamp, frontmatter.readera_timestamp);
    }

    private async Task ImportWord(
        ReadEraWord readEraWord,
        Dictionary<string, string> wordTemplates,
        string fileContent,
        StringBuilder mdoc)
    {
        var comment = readEraWord.Data.WordComment?.Trim() ?? "";
        var templateName = comment.StartsWith('!')
            ? $"{readEraWord.Data.WordColor}_alt"
            : $"{readEraWord.Data.WordColor}";
        var template = wordTemplates.GetValueOrDefault(templateName)
                       ?? wordTemplates.GetValueOrDefault($"{readEraWord.Data.WordColor}");

        if (string.IsNullOrWhiteSpace(template)) return;

        var model = new
        {
            Word = readEraWord.Data.WordTitle,
            Comment = comment.TrimStart('!'),
            Date = DateTime.UnixEpoch
                .AddMilliseconds(readEraWord.Data.WordModifiedTime).ToString("yyyy-MM-dd"),
            Color = readEraWord.Data.WordColor.ToString(),
        };
        var content = await Template.Parse(template).RenderAsync(model);

        if (fileContent.Contains(content)) return;

        mdoc.Append(content);
    }

    private async Task ImportNote(
        ReadEraCitation readEraCitation,
        Dictionary<string, string> citTemplates,
        string fileContent,
        StringBuilder mdoc)
    {
        var noteExtra = readEraCitation.NoteExtra?.Trim() ?? "";
        var noteExtraSplit = noteExtra
            .Split('\n')
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToArray();
        var title = noteExtraSplit.FirstOrDefault() ?? "";
        var details = string.Join('\n', noteExtraSplit.Skip(1));

        var templateName = title.StartsWith('!')
            ? $"{readEraCitation.NoteMark}_alt"
            : $"{readEraCitation.NoteMark}";
        var template = citTemplates.GetValueOrDefault(templateName)
                       ?? citTemplates.GetValueOrDefault($"{readEraCitation.NoteMark}");

        if (string.IsNullOrWhiteSpace(template)) return;

        var model = new
        {
            Quote = readEraCitation.NoteBody,
            NoteHead = title.TrimStart('!'),
            NoteTail = details,
            NoteFull = noteExtra.TrimStart('!'),
            Date = DateTime.UnixEpoch
                .AddMilliseconds(readEraCitation.NoteModifiedTime).ToString("yyyy-MM-dd")
        };

        if (fileContent.Contains(model.Quote)
            && fileContent.Contains(model.NoteHead)
            && fileContent.Contains(model.NoteTail)) return;

        var content = await Template.Parse(template).RenderAsync(model);
        mdoc.Append(content);
    }

    private async Task<ReadEraLibrary> ReadLibraryJson(Stream backupFileStream)
    {
        // ZipArchive uses synchronous version of CopyTo inside,
        // and it causes a "Synchronous operations are disallowed" runtime error
        var memoryStream = new MemoryStream();
        await backupFileStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        var entry = archive.GetEntry("library.json");
        if (entry == null)
        {
            throw new Exception($"ReadEra backup file is invalid");
        }

        var result = await JsonSerializer.DeserializeAsync<ReadEraLibrary>(
            entry.Open(),
            _jsonOptions);
        if (result == null)
        {
            throw new Exception($"ReadEra backup json is invalid");
        }

        return result;
    }
}
