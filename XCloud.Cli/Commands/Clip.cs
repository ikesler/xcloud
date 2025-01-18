using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Microsoft.Extensions.Options;
using XCloud.Clipper.Api.Dto;
using XCloud.Storage.Impl;
using XCloud.Storage.Impl.LocalFS;
using XCloud.Storage.Settings;

namespace XCloud.Cli.Commands;

[Command("clip")]
public class Clip : ICommand
{
    [CommandOption("url", 'u',
        IsRequired = true,
        Description = "Url to clip")]
    public string Url { get; init; } = null!;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var clipper = new Clipper.Impl.Clipper(new StorageDecorator(new LocalFsStorageProvider(Options.Create(new StorageSettings
        {
            LocalFSRoot = @"D:\tmp\epub"
        }))), null!);
        await clipper.Clip(new ClipRequest(new Uri(Url)));
    }
}
