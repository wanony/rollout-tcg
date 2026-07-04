using System.Text.Json;
using System.Text.Json.Serialization;
using TCGTrading.Pricing.Application.Interfaces;

namespace TCGTrading.Pricing.Infrastructure.External;

public sealed class TcgdexPriceClient : ITcgdexPriceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Variant fallback order: prefer holofoil, then reverse-holofoil, then normal,
    // then whatever else is present (some cards only carry one variant).
    private static readonly string[] PreferredVariantOrder = ["holofoil", "reverse-holofoil", "normal"];

    private readonly HttpClient _httpClient;

    public TcgdexPriceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<decimal?> GetMarketPriceUsdAsync(string providerCardId, CancellationToken ct)
    {
        // ponytail: field path (pricing.tcgplayer.<variant>.marketPrice) mirrors what
        // src/Frontend/src/api/pokemontcg.ts + CardDetailModal.tsx already parse from
        // TCGdex v2 card responses; verify against a live TCGdex response before shipping,
        // since TCGdex uses the kebab-case key "reverse-holofoil" (not "reverseHolofoil").
        using var response = await _httpClient.GetAsync(
            $"v2/en/cards/{providerCardId}", ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var card = await response.Content.ReadFromJsonAsync<TcgdexCardResponse>(JsonOptions, ct);

        var tcgplayer = card?.Pricing?.Tcgplayer;
        if (tcgplayer is null)
            return null;

        foreach (var variant in PreferredVariantOrder)
        {
            if (tcgplayer.Variants.TryGetValue(variant, out var tier) && tier.MarketPrice is not null)
                return (decimal)tier.MarketPrice.Value;
        }

        foreach (var tier in tcgplayer.Variants.Values)
        {
            if (tier.MarketPrice is not null)
                return (decimal)tier.MarketPrice.Value;
        }

        return null;
    }

    private sealed class TcgdexCardResponse
    {
        [JsonPropertyName("pricing")]
        public TcgdexPricing? Pricing { get; init; }
    }

    private sealed class TcgdexPricing
    {
        [JsonPropertyName("tcgplayer")]
        public TcgdexTcgplayerPricing? Tcgplayer { get; init; }
    }

    private sealed class TcgdexTcgplayerPricing
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? RawVariants { get; init; }

        [JsonIgnore]
        public Dictionary<string, TcgdexPriceTier> Variants
        {
            get
            {
                var result = new Dictionary<string, TcgdexPriceTier>();
                if (RawVariants is null)
                    return result;

                foreach (var (key, element) in RawVariants)
                {
                    if (key == "unit")
                        continue;

                    if (element.ValueKind != JsonValueKind.Object)
                        continue;

                    var tier = element.Deserialize<TcgdexPriceTier>(JsonOptions);
                    if (tier is not null)
                        result[key] = tier;
                }

                return result;
            }
        }
    }

    private sealed class TcgdexPriceTier
    {
        [JsonPropertyName("marketPrice")]
        public double? MarketPrice { get; init; }
    }
}
