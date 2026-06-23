namespace TCGTrading.CardCatalog.Application.DTOs;

public sealed record CardSearchResult(
    Guid Id,
    string Name,
    string Set,
    string Rarity,
    string Type,
    string Text);
