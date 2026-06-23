using TCGTrading.CardCatalog.Domain.Entities;
using TCGTrading.CardCatalog.Domain.Enums;

namespace TCGTrading.CardCatalog.Infrastructure.Seed;

public static class CardSeed
{
    public static IReadOnlyList<Card> Cards { get; } =
    [
        Card.Create("Dragon Fire",       "Core Set 2024",   Rarity.Rare,       "Spell",    "Deal 3 damage to any target."),
        Card.Create("Shadow Knight",     "Core Set 2024",   Rarity.Uncommon,   "Creature", "First strike. When ~ enters, scry 1."),
        Card.Create("Forest Guardian",   "Wilds of Nature", Rarity.Common,     "Creature", "Reach. Whenever ~ blocks, gain 2 life."),
        Card.Create("Lightning Bolt",    "Starter Pack",    Rarity.Common,     "Spell",    "Deal 1 damage to any target. Fast."),
        Card.Create("Ancient Dragon",    "Wilds of Nature", Rarity.SecretRare, "Creature", "Flying, trample. ETB: deal 5 to each opponent."),
    ];
}
