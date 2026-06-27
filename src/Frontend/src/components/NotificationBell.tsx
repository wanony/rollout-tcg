import { Link } from 'react-router-dom'
import { MessageCircle } from 'lucide-react'
import { useSignalR } from '../hooks/useSignalR'

export default function NotificationBell({ userId }: { userId: string }) {
  const { liveNotifications } = useSignalR(userId)
  const unread = liveNotifications.filter(n => !n.isRead).length

  return (
    <Link to="/notifications" className="relative flex items-center justify-center rounded-lg p-1.5 text-slate-400 transition-colors hover:bg-white/10 hover:text-slate-100">
      <MessageCircle size={18} />
      {unread > 0 && (
        <span className="absolute -top-0.5 -right-0.5 bg-red-500 text-white text-[10px] rounded-full w-4 h-4 flex items-center justify-center">
          {unread}
        </span>
      )}
    </Link>
  )
}
