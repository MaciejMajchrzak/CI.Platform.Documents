using CI.Kernel;
namespace CI.Platform.Documents.Domain.Events;

public sealed record DocumentTemplateCreatedEvent(Guid TemplateId, Guid? TenantId, string Key, string Type) : IEvent;
public sealed record DocumentTemplateUpdatedEvent(Guid TemplateId, Guid? TenantId, string Key) : IEvent;
public sealed record DocumentTemplateDeletedEvent(Guid TemplateId, Guid? TenantId, string Key) : IEvent;
