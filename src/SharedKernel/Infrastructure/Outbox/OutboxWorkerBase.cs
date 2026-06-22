using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace TCGTrading.SharedKernel.Infrastructure.Outbox;

public abstract class OutboxWorkerBase(
    IOutboxRepository outboxRepository,
    IPublishEndpoint publishEndpoint,
    ILogger logger) : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var messages = await outboxRepository.GetUnprocessedAsync(BatchSize, stoppingToken);

            foreach (var message in messages)
            {
                try
                {
                    var type = ResolveEventType(message.EventType);
                    if (type is null)
                    {
                        logger.LogWarning("Unknown outbox event type: {EventType}", message.EventType);
                        continue;
                    }

                    var @event = JsonSerializer.Deserialize(message.Payload, type);
                    if (@event is not null)
                        await publishEndpoint.Publish(@event, type, stoppingToken);

                    await outboxRepository.MarkProcessedAsync(message.Id, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                }
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    // Each service overrides to map event type name → CLR type
    // Return null for unknown types (worker logs warning and skips)
    protected abstract Type? ResolveEventType(string eventType);
}
