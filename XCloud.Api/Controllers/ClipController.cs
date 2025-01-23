using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XCloud.Clipper.Api;
using XCloud.Clipper.Api.Dto;
using XCloud.ReadEra.Impl;

namespace XCloud.Api.Controllers;

[Route("clip")]
[Authorize]
[ApiController]
public class ClipController(IClipper clipper, EBookNotesImporter ebook) : ControllerBase
{
    [HttpPost]
    [Route("article")]
    public async Task<ActionResult> Post([FromBody] ClipRequest clipRequest)
    {
        await clipper.Clip(clipRequest);
        return Ok();
    }

    [HttpPost]
    [Route("bookmark")]
    public async Task<ActionResult> PostBookmark([FromBody] ClipRequest clipRequest)
    {
        await clipper.ClipBookmark(clipRequest);
        return Ok();
    }

    [HttpPost]
    [Route("epub")]
    public async Task<ActionResult> PostEpub([FromBody] ClipRequest clipRequest)
    {
        await clipper.ClipEpub(clipRequest);
        return Ok();
    }

    [HttpPost]
    [Route("readera")]
    public async Task<ActionResult> PostReadera()
    {
        await ebook.ImportFromReadEraBackup(Request.Body);
        return Ok();
    }
}
