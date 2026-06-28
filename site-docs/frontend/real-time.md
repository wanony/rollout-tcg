# Real-time Notifications

The frontend maintains a persistent WebSocket connection to the Notification Service's SignalR hub. When a marketplace event occurs (listing purchased, offer made), the notification bell updates instantly without any polling.

## useSignalR hook

```typescript
// src/hooks/useSignalR.ts
export function useSignalR(userId: string | undefined) {
  const [notifications, setNotifications] = useState<Notification[]>([])

  useEffect(() => {
    if (!userId) return

    const connection = new HubConnectionBuilder()
      .withUrl(`/api/hubs/notifications?userId=${userId}`)
      .withAutomaticReconnect()
      .build()

    connection.on('ReceiveNotification', (notification: Notification) => {
      setNotifications(prev => [notification, ...prev])
    })

    connection.start().catch(console.error)
    return () => { connection.stop() }
  }, [userId])

  return notifications
}
```

`withAutomaticReconnect()` handles transient disconnections — the client retries with exponential backoff and re-registers event handlers automatically.

## NotificationBell

```typescript
const { user } = useAuth()
const notifications = useSignalR(user?.profile.sub)
const unreadCount = notifications.filter(n => !n.isRead).length

// Renders a bell icon with a badge showing unreadCount
```

The badge animates in using framer-motion when `unreadCount` transitions from 0 to non-zero.

## Hub connection path

The SignalR hub is accessed via the API Gateway:

```
WebSocket: ws://localhost/api/hubs/notifications?userId={sub}
              ↓ YARP proxies to
          ws://notification:5004/hubs/notifications?userId={sub}
```

YARP handles the WebSocket upgrade transparently — no special gateway configuration is needed beyond including the route.

## UserIdProvider

The Notification Service maps the `userId` query parameter on the hub connection URL to a SignalR user identity:

```csharp
public class UserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.GetHttpContext()?.Request.Query["userId"].FirstOrDefault();
}
```

This avoids requiring JWT middleware in the Notification Service for v1. The `sub` claim from the user's OIDC session is passed as a query parameter, and the hub uses it to target push calls at the right user.

## Notification lifecycle

1. User logs in — `useSignalR` opens a WebSocket to the hub with `userId=<sub>`
2. User purchases a listing → Marketplace publishes `ListingPurchasedEvent` to RabbitMQ
3. Notification Service consumer creates two `UserNotification` rows in `db_notification`
4. Notification Service calls `IHubContext.Clients.User(buyerId).SendAsync("ReceiveNotification", note)`
5. Hub delivers to the buyer's open WebSocket connection
6. `useSignalR` receives the event and updates `notifications` state
7. `NotificationBell` re-renders with incremented badge count
8. User visits `/notifications` — existing notifications loaded from `GET /api/notifications?userId=<sub>`
9. User clicks a notification — `POST /api/notifications/{id}/read` marks it read
