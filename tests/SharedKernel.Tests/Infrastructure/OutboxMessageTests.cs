using FluentAssertions;
using TCGTrading.SharedKernel.Infrastructure.Outbox;

namespace TCGTrading.SharedKernel.Tests.Infrastructure;

public class OutboxMessageTests
{
    [Fact]
    public void OutboxMessage_DefaultProcessedAt_IsNull()
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "CardPriceUpdatedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow
        };

        message.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void OutboxMessage_CanSetProcessedAt()
    {
        var now = DateTime.UtcNow;
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "CardPriceUpdatedEvent",
            Payload = "{}",
            CreatedAt = now,
            ProcessedAt = now
        };

        message.ProcessedAt.Should().Be(now);
    }

    [Fact]
    public void OutboxMessage_IsUnprocessed_WhenProcessedAtNull()
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "TestEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow
        };

        message.ProcessedAt.HasValue.Should().BeFalse();
    }
}
