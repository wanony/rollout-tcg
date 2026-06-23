import { CollectionItem, PortfolioSummary } from '../types/api'
import { api } from './client'

export async function getPortfolioCards(userId: string): Promise<CollectionItem[]> {
  const { data } = await api.get<CollectionItem[]>(`/portfolio/cards?userId=${userId}`)
  return data
}

export async function getPortfolioSummary(userId: string): Promise<PortfolioSummary> {
  const { data } = await api.get<PortfolioSummary>(`/portfolio/summary?userId=${userId}`)
  return data
}

export async function addCardToPortfolio(payload: {
  userId: string
  cardId: string
  cardName: string
  quantity: number
  condition: string
  acquisitionPriceUsd: number
}): Promise<CollectionItem> {
  const { data } = await api.post<CollectionItem>('/portfolio/cards', payload)
  return data
}

export async function removeCardFromPortfolio(id: string): Promise<void> {
  await api.delete(`/portfolio/cards/${id}`)
}
