using System.Net.Http.Json;
using TCGTrading.CardCatalog.Domain.Entities;
using TCGTrading.CardCatalog.Domain.Enums;

namespace TCGTrading.CardCatalog.Infrastructure.Seed;

public static class CardSeed
{
    private record PtcgSet(string Name);
    private record PtcgCard(string Name, string Supertype, string? Rarity, string? FlavorText, PtcgSet Set);
    private record PtcgResponse(PtcgCard[] Data);

    public static async Task<IReadOnlyList<Card>> FetchAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var cards = new List<Card>();

        for (var page = 1; page <= 2; page++)
        {
            var resp = await http.GetFromJsonAsync<PtcgResponse>(
                $"https://api.pokemontcg.io/v2/cards?pageSize=250&page={page}");
            if (resp?.Data is not { Length: > 0 }) break;
            foreach (var c in resp.Data)
                cards.Add(Card.Create(c.Name, c.Set.Name, MapRarity(c.Rarity), c.Supertype, c.FlavorText ?? string.Empty));
        }

        return cards;
    }

    private static Rarity MapRarity(string? rarity) => rarity switch
    {
        "Common" => Rarity.Common,
        "Uncommon" => Rarity.Uncommon,
        "Rare" or "Rare BREAK" or "Promo" => Rarity.Rare,
        "Rare Holo" or "Rare Holo EX" or "Rare Holo GX" or
        "Rare Holo V" or "Rare Holo VMAX" or "Amazing Rare" => Rarity.UltraRare,
        "Rare Ultra" or "Rare Rainbow" or "Rare Secret" => Rarity.SecretRare,
        _ => Rarity.Common
    };
}
