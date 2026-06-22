using FluentAssertions;
using TCGTrading.SharedKernel.Domain;

namespace TCGTrading.SharedKernel.Tests.Domain;

public class AggregateRootTests
{
    private class TestAggregate : AggregateRoot
    {
        public void RaiseEvent(IDomainEvent @event) => AddDomainEvent(@event);
    }

    private record TestEvent(string Data) : IDomainEvent;

    [Fact]
    public void AddDomainEvent_StoresEvent()
    {
        var aggregate = new TestAggregate();
        aggregate.RaiseEvent(new TestEvent("test"));
        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TestEvent>();
    }

    [Fact]
    public void PopDomainEvents_ReturnsThenClearsEvents()
    {
        var aggregate = new TestAggregate();
        aggregate.RaiseEvent(new TestEvent("a"));
        aggregate.RaiseEvent(new TestEvent("b"));

        var popped = aggregate.PopDomainEvents();

        popped.Should().HaveCount(2);
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void NewAggregate_HasNoEvents()
    {
        var aggregate = new TestAggregate();
        aggregate.DomainEvents.Should().BeEmpty();
    }
}
