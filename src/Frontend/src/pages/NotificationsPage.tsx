import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { useAuth } from '../auth/useAuth'
import { getNotifications, markNotificationRead } from '../api/notifications'

export default function NotificationsPage() {
  const { user } = useAuth()
  const userId = user!.profile.sub
  const qc = useQueryClient()

  const { data: notifications } = useQuery({ queryKey: ['notifications', userId], queryFn: () => getNotifications(userId) })
  const markRead = useMutation({ mutationFn: markNotificationRead, onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications', userId] }) })

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-slate-100 sm:text-2xl">Notifications</h1>
      <div className="space-y-2">
        {notifications?.map((n, i) => (
          <motion.div key={n.id} initial={{ opacity: 0, x: -10 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: i * 0.04 }}
            className={`flex items-start gap-3 rounded-xl border px-4 py-3 backdrop-blur-sm transition-colors
              ${!n.isRead ? 'border-blue-500/50 bg-blue-950/40' : 'border-slate-700/50 bg-slate-900/80'}`}>
            <div className="flex-1 min-w-0">
              <div className={`font-medium text-sm ${!n.isRead ? 'text-blue-200' : 'text-slate-300'}`}>{n.title}</div>
              <div className="text-xs text-slate-500 mt-0.5">{n.message}</div>
              <div className="text-xs text-slate-600 mt-1">{new Date(n.createdAt).toLocaleString()}</div>
            </div>
            {!n.isRead && (
              <button onClick={() => markRead.mutate(n.id)}
                className="shrink-0 rounded-lg px-2.5 py-1.5 text-xs text-blue-400 transition-colors hover:bg-blue-900/40 hover:text-blue-200">
                Read
              </button>
            )}
          </motion.div>
        ))}
        {notifications?.length === 0 && <div className="mt-16 text-center text-slate-500">No notifications yet.</div>}
      </div>
    </div>
  )
}
