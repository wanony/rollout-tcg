import { CardSearchResponse } from '../types/api'
import { api } from './client'

export async function searchCards(
  q?: string,
  set?: string,
  rarity?: string,
  page = 1,
  pageSize = 20,
): Promise<CardSearchResponse> {
  const params = new URLSearchParams()
  if (q) params.set('q', q)
  if (set) params.set('set', set)
  if (rarity) params.set('rarity', rarity)
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))
  const { data } = await api.get<CardSearchResponse>(`/cards/search?${params}`)
  return data
}
