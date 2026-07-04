using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TCGTrading.Alerts.Infrastructure.Persistence;

namespace TCGTrading.Alerts.Infrastructure.Messaging;

public static class MessagingExtensions
{
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        var config = builder.Configuration;
        builder.Services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<AlertsDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.AddConsumer<CardPriceUpdatedConsumer>();

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
