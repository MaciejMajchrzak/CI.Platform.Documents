using CI.Kernel;
using CI.Kernel.Http;
using CI.Platform.Documents.Core.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CI.Platform.Documents.API.Controllers;

[ApiController]
[Route("api/documents/templates")]
[Authorize]
[AllowWithoutModule]
public sealed class TemplatesController(ICommandBus commandBus) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? languageCode = null,
        CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(new ListTemplatesQuery(tenantId, page, pageSize, type, languageCode), ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid? tenantId, CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(new GetTemplateQuery(id, tenantId), ct);
        if (!result.IsSuccess) return NotFound();
        if (HttpContext.IsNotModified(result.Value!.RowVersion)) return StatusCode(304);
        Response.Headers.ETag = ETagHelper.ToETag(result.Value.RowVersion);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTemplateRequest req, CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(new CreateTemplateCommand(
            req.CreatedBy, req.TenantId, req.Key, req.Type,
            req.SubjectTemplate, req.BodyTemplate, req.LanguageCode, req.IsDefault), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value, tenantId = req.TenantId }, result.Value)
            : Conflict(result.ErrorCode);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTemplateRequest req, CancellationToken ct = default)
    {
        var current = await commandBus.SendAsync(new GetTemplateQuery(id, req.TenantId), ct);
        if (!current.IsSuccess) return NotFound();
        if (!HttpContext.IfMatchPasses(current.Value!.RowVersion)) return StatusCode(412);

        var result = await commandBus.SendAsync(new UpdateTemplateCommand(
            id, req.TenantId, req.UpdatedBy, req.SubjectTemplate, req.BodyTemplate, req.IsDefault), ct);
        return result.IsSuccess ? NoContent()
            : result.ErrorCode == ErrorCodes.NOT_FOUND ? NotFound()
            : Conflict(result.ErrorCode);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? tenantId, CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(new DeleteTemplateCommand(id, tenantId), ct);
        return result.IsSuccess ? NoContent()
            : result.ErrorCode == ErrorCodes.NOT_FOUND ? NotFound()
            : Conflict(result.ErrorCode);
    }
}

public record CreateTemplateRequest(
    Guid CreatedBy,
    Guid? TenantId,
    string Key,
    string Type,
    string? SubjectTemplate,
    string BodyTemplate,
    string LanguageCode,
    bool IsDefault);

public record UpdateTemplateRequest(
    Guid? TenantId,
    Guid UpdatedBy,
    string? SubjectTemplate,
    string BodyTemplate,
    bool IsDefault);
