using CI.Kernel;
namespace CI.Platform.Documents.Core;

public interface IDocumentsOutbox
{
    Task WriteAsync(Guid? tenantId, IEvent evt, CancellationToken ct = default);
}
