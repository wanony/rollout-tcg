namespace TCGTrading.SharedKernel.Infrastructure.Outbox;

public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct);
    Task MarkProcessedAsync(Guid messageId, CancellationToken ct);
    Task SaveAsync(OutboxMessage message, CancellationToken ct);
}
