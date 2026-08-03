using CI.Kernel;
using CI.Platform.Documents.Core.DTOs;
using CI.Platform.Documents.Domain.Entities;
namespace CI.Platform.Documents.Core;

public interface IDocumentsRepository
{
    Task<DocumentTemplate?> FindTemplateAsync(Guid templateId, Guid? tenantId, CancellationToken ct = default);
    Task<DocumentTemplate?> FindTemplateByKeyAsync(string key, string type, string languageCode, Guid? tenantId, CancellationToken ct = default);
    Task<PagedResult<DocumentTemplateDto>> ListTemplatesAsync(Guid? tenantId, int page, int pageSize, string? type, string? languageCode, CancellationToken ct = default);
    Task AddTemplateAsync(DocumentTemplate template, CancellationToken ct = default);
    void RemoveTemplate(DocumentTemplate template);
    Task<Result> SaveChangesAsync(CancellationToken ct = default);
}
