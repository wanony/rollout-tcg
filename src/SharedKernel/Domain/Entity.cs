namespace TCGTrading.SharedKernel.Domain;

public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected init; } = DateTime.UtcNow;
}
