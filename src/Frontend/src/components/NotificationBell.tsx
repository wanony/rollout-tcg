import { Link } from 'react-router-dom'
import { useSignalR } from '../hooks/useSignalR'

export default function NotificationBell({ userId }: { userId: string }) {
  const { liveNotifications } = useSignalR(userId)
  const unread = liveNotifications.filter(n => !n.isRead).length

  return (
    <Link to="/notifications" className="relative">
      <span>🔔</span>
      {unread > 0 && (
        <span className="absolute -top-1 -right-1 bg-red-500 text-white text-xs rounded-full w-4 h-4 flex items-center justify-center">
          {unread}
        </span>
      )}
    </Link>
  )
}
