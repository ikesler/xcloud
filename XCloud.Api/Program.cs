using System.Net.Http.Headers;
using System.Text;
using DotNetEnv;
using DotNetEnv.Configuration;
using Microsoft.AspNetCore.Diagnostics;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Http.HttpClients;
using VaultSharp.Extensions.Configuration;
using XCloud.Api.Logging;
using XCloud.Core;

var builder = WebApplication.CreateBuilder();
builder.Configuration.AddDotNetEnv(".env", LoadOptions.TraversePath());

var vaultEndpoint = builder.Configuration.GetValue<string>("Vault:Endpoint");
var vaultToken = builder.Configuration.GetValue<string>("Vault:Token");

if (!string.IsNullOrWhiteSpace(vaultEndpoint) && !string.IsNullOrWhiteSpace(vaultToken))
{
    builder.Services.AddHostedService<VaultChangeWatcher>();
    builder.Configuration.AddVaultConfiguration(() => new VaultOptions(
        vaultEndpoint,
        vaultToken,
        reloadOnChange: true,
        reloadCheckIntervalSeconds: 60), "xcloud", "kv");
}

Serilog.Debugging.SelfLog.Enable(Console.WriteLine);
builder.Host.UseSerilog((ctx, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console(new RenderedCompactJsonFormatter());

    if (ctx.HostingEnvironment.IsProduction())
    {
        var uri = ctx.Configuration.GetValue<string>("Logging:ntfy:url")
            ?? throw new Exception("Missing NTFY url.");
        var username = ctx.Configuration.GetValue<string>("Logging:ntfy:username");
        var password = ctx.Configuration.GetValue<string>("Logging:ntfy:password");
        var authenticationString = $"{username}:{password}";
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));
        var topic = ctx.Configuration.GetValue<string>("Logging:ntfy:topic")
            ?? throw new Exception("Missing NTFY topic.");

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        loggerConfig.WriteTo.Http(
            requestUri: uri,
            httpClient: new JsonHttpClient(httpClient),
            queueLimitBytes: null,
            textFormatter: new NtfyLogFormatter(),
            batchFormatter: new NtfyBatchFormatter(topic),
            restrictedToMinimumLevel: LogEventLevel.Error);
    }
});

builder.Services.AddModules(builder.Configuration,
    typeof(XCloud.Api.Module),
    typeof(XCloud.Storage.Module),
    typeof(XCloud.Sharing.Module),
    typeof(XCloud.Clipper.Module),
    typeof(XCloud.ReadEra.Module),
    typeof(XCloud.Automations.Module),
    typeof(XCloud.Common.Module)
);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/plain";

        var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (exceptionHandlerFeature != null)
        {
            Log.Error(exceptionHandlerFeature.Error, "An unhandled exception occurred.");

            throw exceptionHandlerFeature.Error;
        }

        return Task.CompletedTask;
    });
});

app.UseCors();

app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseRequestLocalization(o =>
{
    o.AddSupportedCultures("en", "ru");
    o.AddSupportedUICultures("en", "ru");
});
app.MapControllers();
app.UseStaticFiles();

Log.Information("Starting application");

app.Run();
