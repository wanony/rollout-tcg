import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { Notification } from '../types/api'

export function useSignalR(userId: string | undefined) {
  const [liveNotifications, setLiveNotifications] = useState<Notification[]>([])

  useEffect(() => {
    if (!userId) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`/api/hubs/notifications?userId=${userId}`)
      .withAutomaticReconnect()
      .build()

    connection.on('notification', (notification: Notification) => {
      setLiveNotifications(prev => [notification, ...prev])
    })

    connection.start().catch(err => console.warn('SignalR connect error:', err))

    return () => { connection.stop() }
  }, [userId])

  return { liveNotifications }
}
