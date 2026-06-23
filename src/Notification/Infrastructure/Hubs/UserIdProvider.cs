using Microsoft.AspNetCore.SignalR;

namespace TCGTrading.Notification.Infrastructure.Hubs;

// ponytail: userId from query string; replace with JWT sub claim when auth middleware is added
public class UserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.GetHttpContext()?.Request.Query["userId"].ToString();
}
