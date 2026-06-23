using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCGTrading.Notification.Application.Interfaces;

namespace TCGTrading.Notification.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<NotificationDbContext>(opts =>
            opts.UseNpgsql(builder.Configuration.GetConnectionString("NotificationDb")));
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        return builder;
    }
}
