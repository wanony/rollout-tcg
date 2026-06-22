namespace TCGTrading.SharedKernel.Contracts;

public abstract record IntegrationEvent(Guid EventId, DateTime OccurredAt);
