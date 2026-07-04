using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCGTrading.Alerts.Application.Interfaces;

namespace TCGTrading.Alerts.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<AlertsDbContext>(opts =>
            opts.UseNpgsql(builder.Configuration.GetConnectionString("AlertsDb")));
        builder.Services.AddScoped<IAlertRepository, AlertRepository>();
        return builder;
    }
}
