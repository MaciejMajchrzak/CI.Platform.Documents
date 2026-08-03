using CI.Kernel;
using CI.Platform.Documents.Core;
using CI.Platform.Documents.Core.Commands;
using CI.Platform.Documents.Core.Handlers;
using CI.Platform.Documents.Domain.Entities;
using CI.Platform.Documents.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CI.Platform.Documents.Tests;

public sealed class DocumentsHandlerTests : IDisposable
{
    private static readonly Guid AdminId   = Guid.NewGuid();
    private static readonly Guid TenantId  = Guid.NewGuid();

    private readonly DocumentsDbContext    _db;
    private readonly IDocumentsOutbox      _outbox;
    private readonly DocumentsRepository   _repo;

    public DocumentsHandlerTests()
    {
        var opts = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db     = new DocumentsDbContext(opts);
        _outbox = new DocumentsOutbox(_db);
        _repo   = new DocumentsRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreateTemplateAsync(
        string key = "invoice",
        string type = TemplateType.Email,
        string lang = "en",
        Guid? tenantId = null,
        bool isDefault = false,
        string body = "Hello {{name}}",
        string? subject = "Subject {{name}}")
    {
        var handler = new CreateTemplateHandler(_repo, _outbox);
        var result  = await handler.HandleAsync(new CreateTemplateCommand(
            AdminId, tenantId, key, type, subject, body, lang, isDefault));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    // ── CreateTemplate ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTemplate_returns_id()
    {
        var id = await CreateTemplateAsync();
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task CreateTemplate_sets_IsDefault()
    {
        var id     = await CreateTemplateAsync(isDefault: true);
        var result = await new GetTemplateHandler(_repo).HandleAsync(new GetTemplateQuery(id, null));
        Assert.True(result.Value!.IsDefault);
    }

    [Fact]
    public async Task CreateTemplate_platform_default_has_null_TenantId()
    {
        var id     = await CreateTemplateAsync(tenantId: null);
        var result = await new GetTemplateHandler(_repo).HandleAsync(new GetTemplateQuery(id, null));
        Assert.Null(result.Value!.TenantId);
    }

    [Fact]
    public async Task CreateTemplate_tenant_specific_has_TenantId()
    {
        var id     = await CreateTemplateAsync(tenantId: TenantId);
        var result = await new GetTemplateHandler(_repo).HandleAsync(new GetTemplateQuery(id, TenantId));
        Assert.Equal(TenantId, result.Value!.TenantId);
    }

    [Fact]
    public async Task CreateTemplate_writes_outbox_event()
    {
        await CreateTemplateAsync();
        Assert.Equal(1, _db.OutboxMessages.Count(m => m.EventType.Contains("DocumentTemplateCreatedEvent")));
    }

    // ── UpdateTemplate ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTemplate_changes_body()
    {
        var id = await CreateTemplateAsync(body: "Old body");
        var result = await new UpdateTemplateHandler(_repo, _outbox).HandleAsync(
            new UpdateTemplateCommand(id, null, AdminId, null, "New body", false));
        Assert.True(result.IsSuccess);

        var fetched = await new GetTemplateHandler(_repo).HandleAsync(new GetTemplateQuery(id, null));
        Assert.Equal("New body", fetched.Value!.BodyTemplate);
    }

    [Fact]
    public async Task UpdateTemplate_wrong_tenant_returns_NOT_FOUND()
    {
        var id = await CreateTemplateAsync(tenantId: TenantId);
        // Pass a different tenantId — should not find template
        var result = await new UpdateTemplateHandler(_repo, _outbox).HandleAsync(
            new UpdateTemplateCommand(id, Guid.NewGuid(), AdminId, null, "x", false));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateTemplate_writes_outbox_event()
    {
        var id = await CreateTemplateAsync();
        await new UpdateTemplateHandler(_repo, _outbox).HandleAsync(
            new UpdateTemplateCommand(id, null, AdminId, null, "new", false));
        Assert.Equal(1, _db.OutboxMessages.Count(m => m.EventType.Contains("DocumentTemplateUpdatedEvent")));
    }

    // ── DeleteTemplate ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTemplate_removes_template()
    {
        var id     = await CreateTemplateAsync();
        var result = await new DeleteTemplateHandler(_repo, _outbox).HandleAsync(new DeleteTemplateCommand(id, null));
        Assert.True(result.IsSuccess);

        var fetched = await new GetTemplateHandler(_repo).HandleAsync(new GetTemplateQuery(id, null));
        Assert.False(fetched.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, fetched.ErrorCode);
    }

    [Fact]
    public async Task DeleteTemplate_wrong_id_returns_NOT_FOUND()
    {
        var result = await new DeleteTemplateHandler(_repo, _outbox).HandleAsync(
            new DeleteTemplateCommand(Guid.NewGuid(), null));
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteTemplate_writes_outbox_event()
    {
        var id = await CreateTemplateAsync();
        await new DeleteTemplateHandler(_repo, _outbox).HandleAsync(new DeleteTemplateCommand(id, null));
        Assert.Equal(1, _db.OutboxMessages.Count(m => m.EventType.Contains("DocumentTemplateDeletedEvent")));
    }

    // ── RenderTemplate ────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderTemplate_substitutes_body_placeholders()
    {
        await CreateTemplateAsync(key: "welcome", body: "Hello {{name}}", subject: null);
        var result = await new RenderTemplateHandler(_repo).HandleAsync(
            new RenderTemplateCommand(null, "welcome", TemplateType.Email, "en", """{"name":"Alice"}"""));
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello Alice", result.Value!.Body);
    }

    [Fact]
    public async Task RenderTemplate_substitutes_subject_placeholders()
    {
        await CreateTemplateAsync(key: "invoice", body: "Body", subject: "Invoice for {{company}}");
        var result = await new RenderTemplateHandler(_repo).HandleAsync(
            new RenderTemplateCommand(null, "invoice", TemplateType.Email, "en", """{"company":"Acme"}"""));
        Assert.Equal("Invoice for Acme", result.Value!.Subject);
    }

    [Fact]
    public async Task RenderTemplate_falls_back_to_platform_default()
    {
        // Platform default (null TenantId)
        await CreateTemplateAsync(key: "hello", tenantId: null, body: "Default body");

        // Render with a tenant — should fall back to platform default
        var result = await new RenderTemplateHandler(_repo).HandleAsync(
            new RenderTemplateCommand(TenantId, "hello", TemplateType.Email, "en", "{}"));
        Assert.True(result.IsSuccess);
        Assert.Equal("Default body", result.Value!.Body);
    }

    [Fact]
    public async Task RenderTemplate_prefers_tenant_specific_over_default()
    {
        await CreateTemplateAsync(key: "hello", tenantId: null,     body: "Default body");
        await CreateTemplateAsync(key: "hello", tenantId: TenantId, body: "Tenant body");

        var result = await new RenderTemplateHandler(_repo).HandleAsync(
            new RenderTemplateCommand(TenantId, "hello", TemplateType.Email, "en", "{}"));
        Assert.Equal("Tenant body", result.Value!.Body);
    }

    [Fact]
    public async Task RenderTemplate_NOT_FOUND_when_no_template()
    {
        var result = await new RenderTemplateHandler(_repo).HandleAsync(
            new RenderTemplateCommand(null, "missing", TemplateType.Email, "en", "{}"));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    [Fact]
    public async Task RenderTemplate_returns_correct_Type()
    {
        await CreateTemplateAsync(key: "doc", type: TemplateType.Pdf, body: "<html/>");
        var result = await new RenderTemplateHandler(_repo).HandleAsync(
            new RenderTemplateCommand(null, "doc", TemplateType.Pdf, "en", "{}"));
        Assert.Equal(TemplateType.Pdf, result.Value!.Type);
    }

    // ── GetTemplate ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTemplate_returns_dto()
    {
        var id     = await CreateTemplateAsync(key: "invoice", body: "Body", subject: "Subj");
        var result = await new GetTemplateHandler(_repo).HandleAsync(new GetTemplateQuery(id, null));
        Assert.True(result.IsSuccess);
        Assert.Equal("invoice", result.Value!.Key);
        Assert.Equal("Body", result.Value.BodyTemplate);
    }

    [Fact]
    public async Task GetTemplate_NOT_FOUND_for_wrong_tenant()
    {
        var id     = await CreateTemplateAsync(tenantId: TenantId);
        var result = await new GetTemplateHandler(_repo).HandleAsync(new GetTemplateQuery(id, Guid.NewGuid()));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    [Fact]
    public async Task GetTemplate_NOT_FOUND_for_unknown_id()
    {
        var result = await new GetTemplateHandler(_repo).HandleAsync(new GetTemplateQuery(Guid.NewGuid(), null));
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    // ── ListTemplates ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListTemplates_includes_platform_defaults_and_tenant()
    {
        await CreateTemplateAsync(key: "a", tenantId: null);                // platform default
        await CreateTemplateAsync(key: "b", tenantId: TenantId);            // tenant-specific
        await CreateTemplateAsync(key: "c", tenantId: Guid.NewGuid());      // different tenant

        var result = await new ListTemplatesHandler(_repo).HandleAsync(
            new ListTemplatesQuery(TenantId, 1, 50, null, null));
        Assert.True(result.IsSuccess);
        // Should see platform default ("a") + own tenant ("b"), NOT the other tenant's ("c")
        Assert.Equal(2, result.Value!.TotalCount);
    }

    [Fact]
    public async Task ListTemplates_filters_by_type()
    {
        await CreateTemplateAsync(key: "e1", type: TemplateType.Email);
        await CreateTemplateAsync(key: "p1", type: TemplateType.Pdf);

        var result = await new ListTemplatesHandler(_repo).HandleAsync(
            new ListTemplatesQuery(null, 1, 50, TemplateType.Email, null));
        Assert.Equal(1, result.Value!.TotalCount);
        Assert.All(result.Value.Items, i => Assert.Equal(TemplateType.Email, i.Type));
    }

    [Fact]
    public async Task ListTemplates_filters_by_languageCode()
    {
        await CreateTemplateAsync(key: "w1", lang: "en");
        await CreateTemplateAsync(key: "w2", lang: "pl");

        var result = await new ListTemplatesHandler(_repo).HandleAsync(
            new ListTemplatesQuery(null, 1, 50, null, "pl"));
        Assert.Equal(1, result.Value!.TotalCount);
        Assert.All(result.Value.Items, i => Assert.Equal("pl", i.LanguageCode));
    }

    [Fact]
    public async Task ListTemplates_paging_works()
    {
        for (var i = 1; i <= 5; i++)
            await CreateTemplateAsync(key: $"tpl{i}");

        var page1 = await new ListTemplatesHandler(_repo).HandleAsync(
            new ListTemplatesQuery(null, 1, 2, null, null));
        var page2 = await new ListTemplatesHandler(_repo).HandleAsync(
            new ListTemplatesQuery(null, 2, 2, null, null));

        Assert.Equal(5, page1.Value!.TotalCount);
        Assert.Equal(2, page1.Value.Items.Count);
        Assert.Equal(2, page2.Value!.Items.Count);
    }

    // ── Outbox — all three mutation handlers write exactly one message ─────────

    [Fact]
    public async Task Create_writes_one_outbox_message()
    {
        await CreateTemplateAsync();
        Assert.Equal(1, _db.OutboxMessages.Count());
    }

    [Fact]
    public async Task Update_writes_one_outbox_message()
    {
        var id = await CreateTemplateAsync();
        _db.OutboxMessages.RemoveRange(_db.OutboxMessages.ToList());
        await _db.SaveChangesAsync();

        await new UpdateTemplateHandler(_repo, _outbox).HandleAsync(
            new UpdateTemplateCommand(id, null, AdminId, null, "x", false));
        Assert.Equal(1, _db.OutboxMessages.Count());
    }

    [Fact]
    public async Task Delete_writes_one_outbox_message()
    {
        var id = await CreateTemplateAsync();
        _db.OutboxMessages.RemoveRange(_db.OutboxMessages.ToList());
        await _db.SaveChangesAsync();

        await new DeleteTemplateHandler(_repo, _outbox).HandleAsync(new DeleteTemplateCommand(id, null));
        Assert.Equal(1, _db.OutboxMessages.Count());
    }

    [Fact]
    public async Task Create_outbox_message_has_correct_EventType()
    {
        await CreateTemplateAsync(tenantId: TenantId);
        var msg = _db.OutboxMessages.Single();
        Assert.Contains("DocumentTemplateCreatedEvent", msg.EventType);
        Assert.Equal(TenantId, msg.TenantId);
    }

    [Fact]
    public async Task Update_outbox_message_has_correct_EventType()
    {
        var id = await CreateTemplateAsync();
        await new UpdateTemplateHandler(_repo, _outbox).HandleAsync(
            new UpdateTemplateCommand(id, null, AdminId, null, "x", false));
        var msg = _db.OutboxMessages.OrderByDescending(m => m.CreatedAt).First();
        Assert.Contains("DocumentTemplateUpdatedEvent", msg.EventType);
    }

    [Fact]
    public async Task Delete_outbox_message_has_correct_EventType()
    {
        var id = await CreateTemplateAsync();
        await new DeleteTemplateHandler(_repo, _outbox).HandleAsync(new DeleteTemplateCommand(id, null));
        var msg = _db.OutboxMessages.OrderByDescending(m => m.CreatedAt).First();
        Assert.Contains("DocumentTemplateDeletedEvent", msg.EventType);
    }
}
