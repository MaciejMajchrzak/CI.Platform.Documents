using System.Text.Json;
using CI.Kernel;
using CI.Platform.Documents.Core;
using CI.Platform.Documents.Domain.Entities;
namespace CI.Platform.Documents.Infrastructure;

public sealed class DocumentsOutbox(DocumentsDbContext db) : IDocumentsOutbox
{
    public async Task WriteAsync(Guid? tenantId, IEvent evt, CancellationToken ct = default)
    {
        var msg = new DocumentsOutboxMessage
        {
            TenantId  = tenantId ?? Guid.Empty,
            EventType = evt.GetType().FullName!,
            Payload   = JsonSerializer.Serialize(evt, evt.GetType()),
        };
        await db.OutboxMessages.AddAsync(msg, ct);
    }
}
