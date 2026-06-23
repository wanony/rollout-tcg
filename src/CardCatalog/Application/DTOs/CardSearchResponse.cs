namespace TCGTrading.CardCatalog.Application.DTOs;

public sealed record CardSearchResponse(
    IReadOnlyList<CardSearchResult> Items,
    long Total,
    int Page,
    int PageSize);
