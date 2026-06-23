using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TCGTrading.Notification.Infrastructure.Messaging;

namespace TCGTrading.Notification.Infrastructure.Messaging;

public static class MessagingExtensions
{
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        var config = builder.Configuration;
        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<ListingPurchasedConsumer>();
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(config["RabbitMq:Host"]!, "/", h =>
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
