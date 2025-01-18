using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Microsoft.Extensions.Options;
using Polly.Registry;
using XCloud.Clipper.Api.Dto;
using XCloud.ReadEra.Constants;
using XCloud.ReadEra.Impl;
using XCloud.Storage.Impl;
using XCloud.Storage.Impl.LocalFS;
using XCloud.Storage.Settings;

namespace XCloud.Cli.Commands;

[Command("clip-readera")]
public class ClipReadEra : ICommand
{
    [CommandOption("path", 'p',
        IsRequired = true,
        Description = "ReadEra backup file (*.bak)")]
    public required string Path { get; init; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var polly = new ResiliencePipelineRegistry<string>();
        polly.GetOrAddPipeline(PollyPipelines.ReadEraImportBookmarks, p => p.Build());
        var ebook = new EBookNotesImporter(polly,
            new StorageDecorator(new LocalFsStorageProvider(Options.Create(new StorageSettings
            {
                LocalFSRoot = @"D:\xhome",
                ReadOnly = false,
            })))
        );
        await ebook.ImportFromReadEraBackup(File.OpenRead(Path));
    }
}
