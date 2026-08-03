using CI.Kernel;
using CI.Platform.Documents.Core.DTOs;
namespace CI.Platform.Documents.Core.Commands;

public sealed record CreateTemplateCommand(
    Guid CreatedBy,
    Guid? TenantId,
    string Key,
    string Type,
    string? SubjectTemplate,
    string BodyTemplate,
    string LanguageCode,
    bool IsDefault) : ICommand<Guid>;

public sealed record UpdateTemplateCommand(
    Guid TemplateId,
    Guid? TenantId,
    Guid UpdatedBy,
    string? SubjectTemplate,
    string BodyTemplate,
    bool IsDefault) : ICommand;

public sealed record DeleteTemplateCommand(
    Guid TemplateId,
    Guid? TenantId) : ICommand;

public sealed record RenderTemplateCommand(
    Guid? TenantId,
    string TemplateKey,
    string Type,
    string LanguageCode,
    string DataJson) : ICommand<RenderedDocumentDto>;

public sealed record GetTemplateQuery(
    Guid TemplateId,
    Guid? TenantId) : ICommand<DocumentTemplateDto>;

public sealed record ListTemplatesQuery(
    Guid? TenantId,
    int Page,
    int PageSize,
    string? Type,
    string? LanguageCode) : ICommand<PagedResult<DocumentTemplateDto>>;
