import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import * as signalR from '@microsoft/signalr'

export function useSignalR(userId: string | undefined) {
  const qc = useQueryClient()

  useEffect(() => {
    if (!userId) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`/api/hubs/notifications?userId=${userId}`)
      .withAutomaticReconnect()
      .build()

    connection.on('notification', () => {
      qc.invalidateQueries({ queryKey: ['notifications', userId] })
    })

    connection.start().catch(err => console.warn('SignalR connect error:', err))

    return () => { connection.stop() }
  }, [userId, qc])
}
