namespace TCGTrading.SharedKernel.Infrastructure.Outbox;

public class OutboxMessage
{
    public Guid Id { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
}
