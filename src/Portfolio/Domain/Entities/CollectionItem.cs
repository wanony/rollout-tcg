using TCGTrading.SharedKernel.Domain;

namespace TCGTrading.Portfolio.Domain.Entities;

public class CollectionItem : AggregateRoot
{
    public Guid UserId { get; private init; }
    public Guid CardId { get; private init; }
    public string CardName { get; private init; } = string.Empty;
    public int Quantity { get; private init; }
    // ponytail: string condition; use CardPhysicalCondition from SharedKernel when validation needed
    public string Condition { get; private init; } = string.Empty;
    public decimal AcquisitionPriceUsd { get; private init; }

    private CollectionItem() { } // EF Core

    public static CollectionItem Create(
        Guid userId, Guid cardId, string cardName,
        int quantity, string condition, decimal acquisitionPriceUsd)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(acquisitionPriceUsd);
        return new CollectionItem
        {
            UserId = userId,
            CardId = cardId,
            CardName = cardName,
            Quantity = quantity,
            Condition = condition,
            AcquisitionPriceUsd = acquisitionPriceUsd
        };
    }
}
