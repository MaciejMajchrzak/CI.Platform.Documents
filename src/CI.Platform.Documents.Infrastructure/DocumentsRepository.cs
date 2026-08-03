using CI.Kernel;
using CI.Platform.Documents.Core;
using CI.Platform.Documents.Core.DTOs;
using CI.Platform.Documents.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CI.Platform.Documents.Infrastructure;

public sealed class DocumentsRepository(DocumentsDbContext db) : IDocumentsRepository
{
    // Guid.Empty is the storage sentinel for "platform default" (null TenantId in the public API)
    private static Guid ToStoredTenantId(Guid? tenantId) => tenantId ?? Guid.Empty;
    private static Guid? ToApiTenantId(Guid tenantId) => tenantId == Guid.Empty ? null : tenantId;

    public Task<DocumentTemplate?> FindTemplateAsync(Guid templateId, Guid? tenantId, CancellationToken ct = default) =>
        db.Templates.FirstOrDefaultAsync(t =>
            t.Id == templateId && t.TenantId == ToStoredTenantId(tenantId), ct);

    public Task<DocumentTemplate?> FindTemplateByKeyAsync(string key, string type, string languageCode, Guid? tenantId, CancellationToken ct = default)
    {
        var storedTenantId = ToStoredTenantId(tenantId);
        return db.Templates.FirstOrDefaultAsync(t =>
            t.Key == key && t.Type == type && t.LanguageCode == languageCode && t.TenantId == storedTenantId, ct);
    }

    public async Task<PagedResult<DocumentTemplateDto>> ListTemplatesAsync(
        Guid? tenantId, int page, int pageSize, string? type, string? languageCode, CancellationToken ct = default)
    {
        var storedTenantId = ToStoredTenantId(tenantId);

        // Include platform defaults (TenantId == Guid.Empty) and tenant-specific templates
        var query = db.Templates.Where(t => t.TenantId == Guid.Empty || t.TenantId == storedTenantId);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(t => t.Type == type);

        if (!string.IsNullOrEmpty(languageCode))
            query = query.Where(t => t.LanguageCode == languageCode);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(t => t.Key)
            .ThenBy(t => t.LanguageCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(t => new DocumentTemplateDto(
            t.Id,
            ToApiTenantId(t.TenantId),
            t.Key, t.Type, t.SubjectTemplate, t.BodyTemplate,
            t.LanguageCode, t.IsDefault, t.CreatedAt, t.RowVersion)).ToList();

        return new PagedResult<DocumentTemplateDto>(dtos, page, pageSize, total);
    }

    public async Task AddTemplateAsync(DocumentTemplate template, CancellationToken ct = default) =>
        await db.Templates.AddAsync(template, ct);

    public void RemoveTemplate(DocumentTemplate template) =>
        db.Templates.Remove(template);

    public async Task<Result> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ErrorCodes.ROWVERSION_CONFLICT);
        }
    }
}
