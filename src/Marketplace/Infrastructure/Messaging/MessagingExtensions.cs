using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TCGTrading.Marketplace.Infrastructure.Messaging;

public static class MessagingExtensions
{
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        var config = builder.Configuration;
        builder.Services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((_, cfg) =>
            {
                cfg.Host(config["RabbitMq:Host"]!, config.GetValue<ushort>("RabbitMq:Port", 5672), "/", h =>
                {
                    h.Username(config["RabbitMq:Username"] ?? "guest");
                    h.Password(config["RabbitMq:Password"] ?? "guest");
                });
            });
        });
        return builder;
    }
}
