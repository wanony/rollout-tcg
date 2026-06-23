using Meilisearch;
using Microsoft.Extensions.Logging;
using TCGTrading.CardCatalog.Application.DTOs;
using TCGTrading.CardCatalog.Application.Interfaces;
using TCGTrading.CardCatalog.Domain.Entities;

namespace TCGTrading.CardCatalog.Infrastructure.Search;

public sealed class MeiliSearchCardSearchService(
    MeilisearchClient client,
    ILogger<MeiliSearchCardSearchService> logger) : ICardSearchService
{
    private const string IndexName = "cards";

    public async Task IndexAsync(Card card, CancellationToken ct = default)
    {
        try
        {
            var index = await EnsureIndexAsync(ct);
            await index.AddDocumentsAsync([ToDocument(card)], cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index card {CardId} ({CardName})", card.Id, card.Name);
        }
    }

    public async Task IndexAllAsync(IReadOnlyList<Card> cards, CancellationToken ct = default)
    {
        if (cards.Count == 0) return;
        try
        {
            var index = await ConfigureIndexAsync(ct);
            var taskInfo = await index.AddDocumentsAsync(
                cards.Select(ToDocument), cancellationToken: ct);
            // Wait for indexing to complete — ensures search works immediately after startup
            await client.WaitForTaskAsync(taskInfo.TaskUid, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bulk index {Count} cards", cards.Count);
        }
    }

    public async Task<CardSearchResponse> SearchAsync(
        string? query,
        string? set,
        string? rarity,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var index = client.Index(IndexName);

        var filters = new List<string>();
        if (!string.IsNullOrEmpty(set))
            filters.Add($"set = \"{set}\"");
        if (!string.IsNullOrEmpty(rarity))
            filters.Add($"rarity = \"{rarity}\"");

        var searchQuery = new SearchQuery
        {
            Filter = filters.Count > 0 ? string.Join(" AND ", filters) : null,
            Offset = (page - 1) * pageSize,
            Limit = pageSize,
        };

        var result = await index.SearchAsync<CardDocument>(
            query ?? string.Empty, searchQuery, ct);

        var searchResult = (SearchResult<CardDocument>)result;

        var items = searchResult.Hits.Select(h => new CardSearchResult(
            Guid.Parse(h.id),
            h.name,
            h.set,
            h.rarity,
            h.type,
            h.text)).ToList();

        return new CardSearchResponse(
            items,
            (long)searchResult.EstimatedTotalHits,
            page,
            pageSize);
    }

    // Lightweight: only ensures the index exists. Used by single-card writes.
    private async Task<Meilisearch.Index> EnsureIndexAsync(CancellationToken ct)
    {
        await client.CreateIndexAsync(IndexName, "id", ct);
        return client.Index(IndexName);
    }

    // Full setup: creates the index and configures all attributes. Used at startup via IndexAllAsync.
    private async Task<Meilisearch.Index> ConfigureIndexAsync(CancellationToken ct)
    {
        await client.CreateIndexAsync(IndexName, "id", ct);
        var index = client.Index(IndexName);
        await index.UpdateSearchableAttributesAsync(
            ["name", "type", "set", "text"], ct);
        await index.UpdateFilterableAttributesAsync(
            ["set", "rarity", "type"], ct);
        await index.UpdateSortableAttributesAsync(["name"], ct);
        return index;
    }

    private static CardDocument ToDocument(Card card) => new(
        card.Id.ToString(),
        card.Name,
        card.Set,
        card.Rarity.ToString(),
        card.Type,
        card.Text);

    // Lowercase field names — MeiliSearch primary key is "id"; field names must match serialized JSON keys
    private sealed record CardDocument(
        string id, string name, string set, string rarity, string type, string text);
}
