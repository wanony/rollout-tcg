const BASE = 'https://api.pokemontcg.io/v2'

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

export interface CardFilters {
  name?: string
}

export async function searchPokemonCards(
  filters: CardFilters = {},
  page = 1,
  pageSize = 20,
): Promise<PokemonCardPage> {
  const params = new URLSearchParams({
    pageSize: String(pageSize),
    page: String(page),
    orderBy: '-set.releaseDate',
  })
  const q = filters.name || ''
  if (q) params.set('q', `name:${q}*`)
  const res = await fetch(`${BASE}/cards?${params}`)
  if (!res.ok) throw new Error(`Pokemon TCG API error: ${res.status}`)
  return res.json()
}
