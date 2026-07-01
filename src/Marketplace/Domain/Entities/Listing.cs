using TCGTrading.Marketplace.Domain.Enums;
using TCGTrading.SharedKernel.Domain;

namespace TCGTrading.Marketplace.Domain.Entities;

public class Listing : AggregateRoot
{
    public Guid SellerId { get; private init; }
    public string CardId { get; private init; } = string.Empty;
    public string CardName { get; private init; } = string.Empty;
    // ponytail: string condition; use CardPhysicalCondition from SharedKernel when validation needed
    public string Condition { get; private init; } = string.Empty;
    public decimal AskingPriceUsd { get; private init; }
    public ListingStatus Status { get; private set; } = ListingStatus.Active;
    public Guid? BuyerId { get; private set; }
    public DateTime? PurchasedAt { get; private set; }

    private Listing() { } // EF Core

    public static Listing Create(
        Guid sellerId, string cardId, string cardName,
        string condition, decimal askingPriceUsd)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(askingPriceUsd);
        return new Listing
        {
            SellerId = sellerId,
            CardId = cardId,
            CardName = cardName,
            Condition = condition,
            AskingPriceUsd = askingPriceUsd
        };
    }

    public void Purchase(Guid buyerId)
    {
        if (Status != ListingStatus.Active)
            throw new InvalidOperationException($"Listing is {Status}, cannot purchase.");
        BuyerId = buyerId;
        Status = ListingStatus.Sold;
        PurchasedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status != ListingStatus.Active)
            throw new InvalidOperationException($"Listing is {Status}, cannot cancel.");
        Status = ListingStatus.Cancelled;
    }
}
