import { Listing } from '../types/api'
import { api } from './client'

export async function getListings(): Promise<Listing[]> {
  const { data } = await api.get<Listing[]>('/listings')
  return data
}

export async function createListing(payload: {
  sellerId: string
  cardId: string
  cardName: string
  condition: string
  askingPriceUsd: number
}): Promise<Listing> {
  const { data } = await api.post<Listing>('/listings', payload)
  return data
}

export async function purchaseListing(id: string, buyerId: string): Promise<Listing> {
  const { data } = await api.post<Listing>(`/listings/${id}/purchase`, { buyerId })
  return data
}

export async function cancelListing(id: string): Promise<void> {
  await api.delete(`/listings/${id}`)
}
