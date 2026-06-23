using TCGTrading.CardCatalog.Domain.Enums;

namespace TCGTrading.CardCatalog.Domain.Entities;

public sealed class Card
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Set { get; private set; } = string.Empty;
    public Rarity Rarity { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;

    private Card() { }

    public static Card Create(string name, string set, Rarity rarity, string type, string text)
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            Name = name,
            Set = set,
            Rarity = rarity,
            Type = type,
            Text = text
        };
    }
}
