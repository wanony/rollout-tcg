const BASE = 'https://api.pokemontcg.io/v2'
const PTCG_KEY = import.meta.env.VITE_PTCG_API_KEY as string | undefined
const headers = PTCG_KEY ? { 'X-Api-Key': PTCG_KEY } : {}

interface PriceTier {
  low: number
  mid: number
  high: number
  market: number
}

export interface PokemonCard {
  id: string
  name: string
  rarity: string
  set: { id: string; name: string; series: string }
  types?: string[]
  hp?: string
  images: { small: string; large: string }
  supertype: string
  tcgplayer?: {
    prices?: {
      normal?: PriceTier
      holofoil?: PriceTier
      reverseHolofoil?: PriceTier
    }
  }
}

export interface PokemonCardPage {
  data: PokemonCard[]
  totalCount: number
  page: number
  pageSize: number
  count: number
}

export interface PokemonSet {
  id: string
  name: string
  series: string
  releaseDate: string
}

export interface CardFilters {
  name?: string
  type?: string
  rarity?: string
  setId?: string
  sort?: 'newest' | 'price-asc' | 'price-desc' | 'name'
}

const ORDER: Record<string, string> = {
  newest:       '-set.releaseDate',
  'price-asc':  'cardmarket.prices.averageSellPrice',
  'price-desc': '-cardmarket.prices.averageSellPrice',
  name:         'name',
}

export async function searchPokemonCards(
  filters: CardFilters = {},
  page = 1,
  pageSize = 20,
): Promise<PokemonCardPage> {
  const parts: string[] = []
  if (filters.name)   parts.push(`name:${filters.name}*`)
  if (filters.type)   parts.push(`types:${filters.type}`)
  if (filters.rarity) parts.push(`rarity:"${filters.rarity}"`)
  if (filters.setId)  parts.push(`set.id:${filters.setId}`)

  const params = new URLSearchParams({
    pageSize: String(pageSize),
    page: String(page),
    orderBy: ORDER[filters.sort ?? 'newest'],
  })
  if (parts.length) params.set('q', parts.join(' '))

  const res = await fetch(`${BASE}/cards?${params}`, { headers })
  if (!res.ok) throw new Error(`Pokemon TCG API error: ${res.status}`)
  return res.json()
}

export async function fetchSets(): Promise<PokemonSet[]> {
  const res = await fetch(`${BASE}/sets?orderBy=-releaseDate&pageSize=250`, { headers })
  if (!res.ok) throw new Error(`Pokemon TCG API error: ${res.status}`)
  const data = await res.json()
  return data.data
}

export async function fetchAutocompleteSuggestions(name: string): Promise<string[]> {
  if (name.length < 2) return []
  const params = new URLSearchParams({ q: `name:${name}*`, pageSize: '6', select: 'name' })
  try {
    const res = await fetch(`${BASE}/cards?${params}`, { headers })
    if (!res.ok) return []
    const data: PokemonCardPage = await res.json()
    return [...new Set(data.data.map(c => c.name))].slice(0, 5)
  } catch {
    return []
  }
}
