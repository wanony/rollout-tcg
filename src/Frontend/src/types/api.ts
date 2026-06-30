export interface CollectionItem {
  id: string
  userId: string
  cardId: string
  cardName: string
  quantity: number
  condition: string
  acquisitionPriceUsd: number
  createdAt: string
}

export interface PortfolioSummary {
  totalItems: number
  totalCards: number
  totalAcquisitionCostUsd: number
}

export interface Listing {
  id: string
  sellerId: string
  cardId: string
  cardName: string
  condition: string
  askingPriceUsd: number
  status: 'Active' | 'Sold' | 'Cancelled'
  buyerId?: string
  purchasedAt?: string
  createdAt: string
}

export interface Offer {
  id: string
  listingId: string
  buyerId: string
  offeredPriceUsd: number
  status: 'Pending' | 'Accepted' | 'Rejected'
  createdAt: string
  respondedAt?: string
}

export interface CardSearchResult {
  id: string
  name: string
  set: string
  rarity: string
  type: string
  text: string
}

export interface CardSearchResponse {
  items: CardSearchResult[]
  total: number
  page: number
  pageSize: number
}

export interface Notification {
  id: string
  userId: string
  title: string
  message: string
  type: string
  referenceId?: string
  isRead: boolean
  createdAt: string
}
