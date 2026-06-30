using TCGTrading.Marketplace.Domain.Enums;
using TCGTrading.SharedKernel.Domain;

namespace TCGTrading.Marketplace.Domain.Entities;

public class Offer : AggregateRoot
{
    public Guid ListingId { get; private init; }
    public Guid BuyerId { get; private init; }
    public decimal OfferedPriceUsd { get; private init; }
    public OfferStatus Status { get; private set; } = OfferStatus.Pending;
    public DateTime? RespondedAt { get; private set; }

    private Offer() { } // EF Core

    public static Offer Create(Guid listingId, Guid buyerId, decimal offeredPriceUsd)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(offeredPriceUsd);
        return new Offer
        {
            ListingId = listingId,
            BuyerId = buyerId,
            OfferedPriceUsd = offeredPriceUsd
        };
    }

    public void Accept()
    {
        if (Status != OfferStatus.Pending)
            throw new InvalidOperationException($"Offer is {Status}, cannot accept.");
        Status = OfferStatus.Accepted;
        RespondedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        if (Status != OfferStatus.Pending)
            throw new InvalidOperationException($"Offer is {Status}, cannot reject.");
        Status = OfferStatus.Rejected;
        RespondedAt = DateTime.UtcNow;
    }
}
