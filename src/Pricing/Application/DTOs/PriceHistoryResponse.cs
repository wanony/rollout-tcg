using TCGTrading.Pricing.Domain.ValueObjects;

namespace TCGTrading.Pricing.Application.DTOs;

public sealed record PriceHistoryResponse(
    Guid CardId,
    string Granularity,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<PriceBucket> Buckets);
