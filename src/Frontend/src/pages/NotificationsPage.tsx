import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { getNotifications, markNotificationRead } from '../api/notifications'

export default function NotificationsPage() {
  const { user } = useAuth()
  const userId = user!.profile.sub
  const qc = useQueryClient()

  const { data: notifications } = useQuery({
    queryKey: ['notifications', userId],
    queryFn: () => getNotifications(userId),
  })

  const markRead = useMutation({
    mutationFn: markNotificationRead,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications', userId] }),
  })

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Notifications</h1>
      <div className="space-y-2">
        {notifications?.map(n => (
          <div key={n.id}
            className={`bg-white border rounded px-4 py-3 flex items-start gap-3 ${!n.isRead ? 'border-blue-400' : ''}`}>
            <div className="flex-1">
              <div className={`font-medium ${!n.isRead ? 'text-blue-800' : ''}`}>{n.title}</div>
              <div className="text-sm text-gray-600">{n.message}</div>
              <div className="text-xs text-gray-400 mt-1">{new Date(n.createdAt).toLocaleString()}</div>
            </div>
            {!n.isRead && (
              <button
                onClick={() => markRead.mutate(n.id)}
                className="text-xs text-blue-600 hover:text-blue-800 whitespace-nowrap"
              >
                Mark read
              </button>
            )}
          </div>
        ))}
        {notifications?.length === 0 && <p className="text-gray-500">No notifications yet.</p>}
      </div>
    </div>
  )
}
