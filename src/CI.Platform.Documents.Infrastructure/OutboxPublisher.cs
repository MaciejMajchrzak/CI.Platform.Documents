using System.Text.Json;
using CI.Kernel;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace CI.Platform.Documents.Infrastructure;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxRetries = 5;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publisher batch error");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db        = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var msg in messages)
        {
            try
            {
                var type = Type.GetType(msg.EventType);
                if (type is null)
                {
                    logger.LogWarning("Cannot resolve type {EventType} — skipping", msg.EventType);
                    msg.Status    = OutboxMessageStatus.Failed;
                    msg.LastError = $"Type not found: {msg.EventType}";
                    continue;
                }

                var payload = JsonSerializer.Deserialize(msg.Payload, type);
                await publisher.Publish(payload!, type, ct);
                msg.Status      = OutboxMessageStatus.Delivered;
                msg.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {Id} ({EventType})", msg.Id, msg.EventType);
                msg.RetryCount++;
                msg.LastError = ex.Message;
                if (msg.RetryCount >= MaxRetries)
                    msg.Status = OutboxMessageStatus.Failed;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
