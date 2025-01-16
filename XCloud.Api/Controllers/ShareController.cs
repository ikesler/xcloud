using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using XCloud.Api.Models;
using XCloud.Helpers;
using XCloud.Sharing.Api;
using XCloud.Sharing.Api.Dto;
using XCloud.Sharing.Api.Dto.Shares;

namespace XCloud.Api.Controllers;

[Route("s")]
public class ShareController(IShareService shareService) : Controller
{
    private const long MaxRangeSize = 5_000_000;
    private const string PasskeyCookie = "passkey";

    [HttpPost]
    [Authorize]
    [Route("")]
    public async Task<IActionResult> Post([FromBody] ShareRequest shareRequest)
    {
        var shareResponse = await shareService.Share(shareRequest.Path);
        if (shareResponse == null) return NotFound();
        return Ok(new ShareResponse { Url = shareResponse });
    }

    private async Task HandleRaw(string[] key)
    {
        long firstByte = 0;
        long? lastByte = null;
        bool rangeRequest = false;
        var ranges = Request.GetTypedHeaders().Range;
        if (ranges != null)
        {
            if (ranges.Ranges.Count > 1)
            {
                Response.StatusCode = (int) HttpStatusCode.RequestedRangeNotSatisfiable;
                return;
            }
            if (ranges.Unit != "bytes")
            {
                Response.StatusCode = (int) HttpStatusCode.RequestedRangeNotSatisfiable;
                return;
            }
            if (ranges.Ranges.Count > 0)
            {
                rangeRequest = true;
                var range = ranges.Ranges.Single();
                firstByte = range.From ?? 0;
                lastByte = range.To;
            }
        }

        if (rangeRequest && (!lastByte.HasValue || lastByte - firstByte > MaxRangeSize))
        {
            lastByte = firstByte + MaxRangeSize - 1;
        }

        Log.Information("Calling storage");
        var rawShare = (RawShare?) await shareService.GetShare(key, firstByte, lastByte, ShareType.Raw, Request.Cookies[PasskeyCookie]);
        Log.Information("Got response from storage");
        if (rawShare == null)
        {
            Response.StatusCode = (int) HttpStatusCode.NotFound;
            return;
        }

        if (lastByte.HasValue && rawShare.TotalBytes.HasValue)
        {
            lastByte = Math.Min(lastByte.Value, rawShare.TotalBytes.Value - 1);
        }
        else if (rawShare.TotalBytes.HasValue)
        {
            lastByte = rawShare.TotalBytes.Value - 1;
        }

        Response.Headers.ContentType = rawShare.ContentType;

        if (rangeRequest)
        {
            var of = rawShare.TotalBytes.HasValue ? $"/{rawShare.TotalBytes}" : "";
            Response.Headers.ContentRange = $"bytes {firstByte}-{lastByte}{of}";
            Response.StatusCode = (int)HttpStatusCode.PartialContent;
        }

        if (rangeRequest && lastByte.HasValue)
        {
            Response.Headers.ContentLength  = lastByte.Value - firstByte + 1;
        }
        else if (rawShare.TotalBytes.HasValue)
        {
            Response.Headers.ContentLength = rawShare.TotalBytes;
        }

        Log.Information("Starting streaming file content");
        await rawShare.Content.CopyTo(Response.Body, firstByte, lastByte);
        Log.Information("Finished streaming file content");
        await rawShare.Content.DisposeAsync();
    }

    [HttpGet]
    [Route("{*shareKey}")]
    public async Task<IActionResult> Get(
        [FromRoute] string shareKey,
        [FromQuery(Name = "f")] ShareType? type = null)
    {
        if (string.IsNullOrWhiteSpace(shareKey)) return BadRequest();

        var pathArray = shareKey.Split('/').ToArray();

        if (type == ShareType.Raw)
        {
            await HandleRaw(pathArray);
            return Empty;
        }

        var passkey = Request.Cookies[PasskeyCookie];

        var share = await shareService.GetShare(pathArray, 0, null, type, passkey);
        return share switch
        {
            null => NotFound(),
            RedirectShare redirectShare => Redirect(redirectShare.Url),
            MarkdownShare markdownShare => View("MarkdownShare", markdownShare),
            RequestPasskeyShare => View("RequestPasskeyShare", new PasskeyViewModel(false)),
            ExcalidrawShare excalidrawShare => View("ExcalidrawShare", excalidrawShare),
            VideoShare videoShare => View("VideoShare", videoShare),
            RawShare rawShare => File(rawShare.Content, rawShare.ContentType),
            _ => throw new Exception($"Unsupported share type: {share.GetType()}"),
        };
    }

    [HttpPost]
    [Route("{*shareKey}")]
    public async Task<IActionResult> EnterPasskey([FromRoute] string shareKey, [FromForm] PasskeyModel passkeyModel)
    {
        if (string.IsNullOrWhiteSpace(shareKey)) return BadRequest();

        var pathArray = shareKey.Split('/').ToArray();
        if (pathArray.Length == 0) return BadRequest();

        var token = await shareService.GetShareAccessToken(pathArray, passkeyModel.Passkey);
        if (token == null)
        {
            return View("RequestPasskeyShare", new PasskeyViewModel(true));
        }

        Response.Cookies.Append(PasskeyCookie, token, new CookieOptions
        {
            Path = $"/s/{shareKey}",
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.Now.AddDays(1),
        });
        var share = await shareService.GetShare(pathArray, 0, null, null, token);
        if (share is not MarkdownShare markdownShare) return NotFound();
        return View("MarkdownShare", markdownShare);
    }

    [HttpDelete]
    [Authorize]
    [Route("{key}")]
    public async Task<IActionResult> Delete(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest();

        await shareService.UnShare(key);

        return Ok();
    }
}
