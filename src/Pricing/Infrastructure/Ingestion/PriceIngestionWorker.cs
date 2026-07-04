using System.Data;
using Dapper;
using MassTransit;
using TCGTrading.Pricing.Application.Interfaces;
using TCGTrading.Pricing.Domain.Entities;
using TCGTrading.Pricing.Infrastructure.Persistence;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.Pricing.Infrastructure.Ingestion;

// ponytail: single-replica advisory-lock scheduler; swap for Quartz clustered scheduling if Pricing ever needs richer/replicated scheduling.
public class PriceIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PriceIngestionWorker> logger) : BackgroundService
{
    // Arbitrary constant advisory-lock key shared by every Pricing replica racing for this tick.
    private const long AdvisoryLockKey = 872_364_501L;

    // Parameterless constructor used by unit tests, which drive RunOnceAsync directly
    // and never touch the scope factory / advisory lock.
    public PriceIngestionWorker(IConfiguration configuration, ILogger<PriceIngestionWorker> logger)
        : this(null!, configuration, logger)
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = configuration.GetValue("Pricing:PollIntervalSeconds", 300);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        do
        {
            try
            {
                await ExecuteTickWithLockAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Price ingestion tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExecuteTickWithLockAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnection>();

        // Dapper auto-opens/closes connections it opens itself around each command, which would
        // return this (pooled) connection to the pool - and reset its session, releasing the
        // advisory lock - between the acquire and the actual work. Opening it explicitly here
        // keeps the SAME session (and lock) alive across acquire -> RunOnceAsync -> unlock; Dapper
        // only auto-closes connections it had to open itself, so an already-open connection is
        // left open by every command that follows. Disposal is still handled by the scope.
        if (db is Npgsql.NpgsqlConnection npg && npg.State != ConnectionState.Open)
        {
            await npg.OpenAsync(ct);
        }

        var acquired = await db.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT pg_try_advisory_lock(@key)",
                new { key = AdvisoryLockKey },
                cancellationToken: ct));

        if (!acquired)
        {
            logger.LogDebug("Skipping price ingestion tick; another replica holds the advisory lock");
            return;
        }

        try
        {
            var priceClient = scope.ServiceProvider.GetRequiredService<ITcgdexPriceClient>();
            var watchedCardRepo = scope.ServiceProvider.GetRequiredService<IWatchedCardRepository>();
            var priceRepo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            var outbox = scope.ServiceProvider.GetRequiredService<PricingOutboxDbContext>();

            await RunOnceAsync(priceClient, watchedCardRepo, priceRepo, publisher, ct);

            // Flush the EF bus outbox: messages published during this tick are only captured
            // against the scoped PricingOutboxDbContext until SaveChanges writes them out.
            await outbox.SaveChangesAsync(ct);
        }
        finally
        {
            await db.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT pg_advisory_unlock(@key)",
                    new { key = AdvisoryLockKey },
                    cancellationToken: CancellationToken.None));
        }
    }

    // Assumes the advisory lock (or test isolation) is already held; drives exactly one ingestion pass.
    public async Task RunOnceAsync(
        ITcgdexPriceClient priceClient,
        IWatchedCardRepository watchedCardRepo,
        IPriceRepository priceRepo,
        IPublishEndpoint publisher,
        CancellationToken ct)
    {
        var watchedCards = await watchedCardRepo.GetAllAsync(ct);

        foreach (var watchedCard in watchedCards)
        {
            await ProcessWatchedCardAsync(watchedCard, priceClient, priceRepo, publisher, ct);
        }
    }

    private async Task ProcessWatchedCardAsync(
        WatchedCard watchedCard,
        ITcgdexPriceClient priceClient,
        IPriceRepository priceRepo,
        IPublishEndpoint publisher,
        CancellationToken ct)
    {
        var price = await priceClient.GetMarketPriceUsdAsync(watchedCard.ProviderCardId, ct);
        if (price is null)
            return;

        // watchedCard.CardId is Guid.Empty for rows created via PriceAlertCreatedConsumer, which
        // doesn't know Pricing's real Guid. Fall back to a stable, deterministic Guid derived from
        // ProviderCardId so every watched card still keys to a distinct, consistent identity.
        var cardId = watchedCard.CardId != Guid.Empty
            ? watchedCard.CardId
            : DeterministicGuid.From(watchedCard.ProviderCardId);

        var latestPrice = await priceRepo.GetLatestPriceAsync(cardId, ct);
        if (latestPrice == price.Value)
            return;

        var snapshot = PriceSnapshot.Create(
            cardId, price.Value, "USD", "tcgdex", DateTimeOffset.UtcNow);
        await priceRepo.AddAsync(snapshot, ct);

        await publisher.Publish(new CardPriceUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cardId,
            watchedCard.ProviderCardId,
            "tcgdex",
            price.Value,
            price.Value,
            price.Value), ct);
    }
}
