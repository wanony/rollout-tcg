using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TCGTrading.Pricing.Infrastructure.Persistence;

namespace TCGTrading.Pricing.Infrastructure.Messaging;

public static class MessagingExtensions
{
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        var config = builder.Configuration;
        builder.Services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<PricingOutboxDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.AddConsumer<PriceAlertCreatedConsumer>();
            x.AddConsumer<PriceAlertDeletedConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(config["RabbitMq:Host"]!, config.GetValue<ushort>("RabbitMq:Port", 5672), "/", h =>
                {
                    h.Username(config["RabbitMq:Username"] ?? "guest");
                    h.Password(config["RabbitMq:Password"] ?? "guest");
                });
                cfg.ConfigureEndpoints(ctx);
            });
        });
        return builder;
    }
}
