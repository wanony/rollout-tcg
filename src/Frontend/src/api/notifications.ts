import { Notification } from '../types/api'
import { api } from './client'

export async function getNotifications(userId: string): Promise<Notification[]> {
  const { data } = await api.get<Notification[]>(`/notifications?userId=${userId}`)
  return data
}

export async function markNotificationRead(id: string): Promise<Notification> {
  const { data } = await api.post<Notification>(`/notifications/${id}/read`)
  return data
}
