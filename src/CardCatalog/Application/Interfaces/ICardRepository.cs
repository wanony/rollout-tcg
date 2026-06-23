using TCGTrading.CardCatalog.Domain.Entities;

namespace TCGTrading.CardCatalog.Application.Interfaces;

public interface ICardRepository
{
    Task<Card> AddAsync(Card card, CancellationToken ct = default);
    Task<IReadOnlyList<Card>> GetAllAsync(CancellationToken ct = default);
}
