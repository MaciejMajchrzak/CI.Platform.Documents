using CI.Kernel;
using CI.Platform.Documents.Core.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CI.Platform.Documents.API.Controllers;

[ApiController]
[Route("api/documents/render")]
[Authorize]
[AllowWithoutModule]
public sealed class RenderController(ICommandBus commandBus) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Render([FromBody] RenderRequest req, CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(new RenderTemplateCommand(
            req.TenantId, req.TemplateKey, req.Type, req.LanguageCode, req.DataJson), ct);
        return result.IsSuccess ? Ok(result.Value)
            : result.ErrorCode == ErrorCodes.NOT_FOUND ? NotFound()
            : BadRequest(result.ErrorCode);
    }
}

public record RenderRequest(
    Guid? TenantId,
    string TemplateKey,
    string Type,
    string LanguageCode,
    string DataJson);
