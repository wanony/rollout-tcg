using TCGTrading.CardCatalog.Application.DTOs;
using TCGTrading.CardCatalog.Domain.Entities;

namespace TCGTrading.CardCatalog.Application.Interfaces;

public interface ICardSearchService
{
    Task IndexAsync(Card card, CancellationToken ct = default);
    Task IndexAllAsync(IReadOnlyList<Card> cards, CancellationToken ct = default);
    Task<CardSearchResponse> SearchAsync(
        string? query,
        string? set,
        string? rarity,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
