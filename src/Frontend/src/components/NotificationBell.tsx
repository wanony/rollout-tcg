import { Link } from 'react-router-dom'
import { MessageCircle } from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { useSignalR } from '../hooks/useSignalR'
import { getNotifications } from '../api/notifications'

export default function NotificationBell({ userId }: { userId: string }) {
  useSignalR(userId)
  const { data: notifications } = useQuery({
    queryKey: ['notifications', userId],
    queryFn: () => getNotifications(userId),
    staleTime: 60_000,
  })
  const unread = notifications?.filter(n => !n.isRead).length ?? 0

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
