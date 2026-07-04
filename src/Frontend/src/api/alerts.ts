import { AlertResponse, CreateAlertRequest } from '../types/api'
import { api } from './client'

export async function createAlert(payload: CreateAlertRequest): Promise<AlertResponse> {
  const { data } = await api.post<AlertResponse>('/alerts', payload)
  return data
}

export async function getAlerts(userId: string): Promise<AlertResponse[]> {
  const { data } = await api.get<AlertResponse[]>(`/alerts?userId=${userId}`)
  return data
}

export async function deleteAlert(id: string, userId: string): Promise<void> {
  await api.delete(`/alerts/${id}?userId=${userId}`)
}
