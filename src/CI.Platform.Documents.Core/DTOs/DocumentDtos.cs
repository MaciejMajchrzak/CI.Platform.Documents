using CI.Kernel;
namespace CI.Platform.Documents.Core.DTOs;

public sealed record DocumentTemplateDto(
    Guid Id,
    Guid? TenantId,
    string Key,
    string Type,
    string? SubjectTemplate,
    string BodyTemplate,
    string LanguageCode,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    uint RowVersion);

public sealed record RenderedDocumentDto(
    string? Subject,
    string Body,
    string Type);
