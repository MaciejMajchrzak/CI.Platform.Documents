using System.Text.Json;
using System.Text.RegularExpressions;
using CI.Kernel;
using CI.Platform.Documents.Core.Commands;
using CI.Platform.Documents.Core.DTOs;
using CI.Platform.Documents.Domain.Entities;
using CI.Platform.Documents.Domain.Events;
namespace CI.Platform.Documents.Core.Handlers;

[NoEvent("event written to outbox")]
public sealed class CreateTemplateHandler(IDocumentsRepository repo, IDocumentsOutbox outbox)
    : ICommandHandler<CreateTemplateCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateTemplateCommand cmd, CancellationToken ct = default)
    {
        var template = new DocumentTemplate
        {
            TenantId        = cmd.TenantId ?? Guid.Empty, // Guid.Empty = platform default
            CreatedBy       = cmd.CreatedBy,
            UpdatedBy       = cmd.CreatedBy,
            Key             = cmd.Key,
            Type            = cmd.Type,
            SubjectTemplate = cmd.SubjectTemplate,
            BodyTemplate    = cmd.BodyTemplate,
            LanguageCode    = cmd.LanguageCode,
            IsDefault       = cmd.IsDefault,
        };
        await repo.AddTemplateAsync(template, ct);
        await outbox.WriteAsync(cmd.TenantId, new DocumentTemplateCreatedEvent(template.Id, cmd.TenantId, cmd.Key, cmd.Type), ct);
        var result = await repo.SaveChangesAsync(ct);
        if (!result.IsSuccess) return Result<Guid>.Failure(result.ErrorCode!);
        return Result<Guid>.Success(template.Id);
    }
}

[NoEvent("event written to outbox")]
public sealed class UpdateTemplateHandler(IDocumentsRepository repo, IDocumentsOutbox outbox)
    : ICommandHandler<UpdateTemplateCommand>
{
    public async Task<Result> HandleAsync(UpdateTemplateCommand cmd, CancellationToken ct = default)
    {
        var template = await repo.FindTemplateAsync(cmd.TemplateId, cmd.TenantId, ct);
        if (template is null) return Result.Failure(ErrorCodes.NOT_FOUND);
        template.SubjectTemplate = cmd.SubjectTemplate;
        template.BodyTemplate    = cmd.BodyTemplate;
        template.IsDefault       = cmd.IsDefault;
        template.UpdatedBy       = cmd.UpdatedBy;
        await outbox.WriteAsync(cmd.TenantId, new DocumentTemplateUpdatedEvent(cmd.TemplateId, cmd.TenantId, template.Key), ct);
        return await repo.SaveChangesAsync(ct);
    }
}

[NoEvent("event written to outbox")]
public sealed class DeleteTemplateHandler(IDocumentsRepository repo, IDocumentsOutbox outbox)
    : ICommandHandler<DeleteTemplateCommand>
{
    public async Task<Result> HandleAsync(DeleteTemplateCommand cmd, CancellationToken ct = default)
    {
        var template = await repo.FindTemplateAsync(cmd.TemplateId, cmd.TenantId, ct);
        if (template is null) return Result.Failure(ErrorCodes.NOT_FOUND);
        repo.RemoveTemplate(template);
        await outbox.WriteAsync(cmd.TenantId, new DocumentTemplateDeletedEvent(cmd.TemplateId, cmd.TenantId, template.Key), ct);
        return await repo.SaveChangesAsync(ct);
    }
}

[NoEvent("query — synchronous render, no event")]
public sealed class RenderTemplateHandler(IDocumentsRepository repo)
    : ICommandHandler<RenderTemplateCommand, RenderedDocumentDto>
{
    public async Task<Result<RenderedDocumentDto>> HandleAsync(RenderTemplateCommand cmd, CancellationToken ct = default)
    {
        var template = await repo.FindTemplateByKeyAsync(cmd.TemplateKey, cmd.Type, cmd.LanguageCode, cmd.TenantId, ct);
        // Fall back to platform default (TenantId = null) if no tenant-specific template
        template ??= await repo.FindTemplateByKeyAsync(cmd.TemplateKey, cmd.Type, cmd.LanguageCode, null, ct);
        if (template is null) return Result<RenderedDocumentDto>.Failure(ErrorCodes.NOT_FOUND);

        var data = string.IsNullOrEmpty(cmd.DataJson)
            ? new Dictionary<string, object>()
            : JsonSerializer.Deserialize<Dictionary<string, object>>(cmd.DataJson) ?? new();

        var subject = template.SubjectTemplate is not null ? Render(template.SubjectTemplate, data) : null;
        var body    = Render(template.BodyTemplate, data);
        return Result<RenderedDocumentDto>.Success(new RenderedDocumentDto(subject, body, template.Type));
    }

    // Minimal Handlebars-style {{key}} substitution
    private static string Render(string template, Dictionary<string, object> data) =>
        Regex.Replace(template, @"\{\{(\w+)\}\}", m =>
            data.TryGetValue(m.Groups[1].Value, out var v) ? v?.ToString() ?? string.Empty : m.Value);
}

[NoEvent("query — read only")]
public sealed class GetTemplateHandler(IDocumentsRepository repo)
    : ICommandHandler<GetTemplateQuery, DocumentTemplateDto>
{
    public async Task<Result<DocumentTemplateDto>> HandleAsync(GetTemplateQuery query, CancellationToken ct = default)
    {
        var t = await repo.FindTemplateAsync(query.TemplateId, query.TenantId, ct);
        if (t is null) return Result<DocumentTemplateDto>.Failure(ErrorCodes.NOT_FOUND);
        return Result<DocumentTemplateDto>.Success(ToDto(t));
    }

    // Convert Guid.Empty back to null (Guid.Empty is the storage sentinel for "platform default")
    private static DocumentTemplateDto ToDto(DocumentTemplate t) => new(
        t.Id,
        t.TenantId == Guid.Empty ? null : t.TenantId,
        t.Key, t.Type, t.SubjectTemplate, t.BodyTemplate, t.LanguageCode, t.IsDefault, t.CreatedAt, t.RowVersion);
}

[NoEvent("query — read only")]
public sealed class ListTemplatesHandler(IDocumentsRepository repo)
    : ICommandHandler<ListTemplatesQuery, PagedResult<DocumentTemplateDto>>
{
    public async Task<Result<PagedResult<DocumentTemplateDto>>> HandleAsync(ListTemplatesQuery query, CancellationToken ct = default)
    {
        var result = await repo.ListTemplatesAsync(query.TenantId, query.Page, query.PageSize, query.Type, query.LanguageCode, ct);
        return Result<PagedResult<DocumentTemplateDto>>.Success(result);
    }
}
