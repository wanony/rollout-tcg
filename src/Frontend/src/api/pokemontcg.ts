const BASE = 'https://api.tcgdex.net/v2/en'

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

// TCGdex raw shapes
interface TcgdexPriceTier {
  lowPrice?: number
  midPrice?: number
  highPrice?: number
  marketPrice?: number
}

interface TcgdexCard {
  id: string
  name: string
  category: string
  rarity?: string
  types?: string[]
  hp?: string
  image?: string
  set: { id: string; name: string; serie?: { name: string } }
  pricing?: {
    tcgplayer?: { normal?: TcgdexPriceTier; holofoil?: TcgdexPriceTier; reverseHolofoil?: TcgdexPriceTier }
  }
}

interface TcgdexSet {
  id: string
  name: string
  serie?: { name: string }
  releaseDate?: string
}

function mapTier(t?: TcgdexPriceTier): PriceTier | undefined {
  if (!t) return undefined
  return { low: t.lowPrice ?? 0, mid: t.midPrice ?? 0, high: t.highPrice ?? 0, market: t.marketPrice ?? 0 }
}

function mapCard(c: TcgdexCard): PokemonCard {
  return {
    id: c.id,
    name: c.name,
    rarity: c.rarity ?? 'Unknown',
    set: { id: c.set.id, name: c.set.name, series: c.set.serie?.name ?? '' },
    types: c.types,
    hp: c.hp,
    images: {
      small: c.image ? `${c.image}/low.webp` : '',
      large: c.image ? `${c.image}/high.webp` : '',
    },
    supertype: c.category,
    tcgplayer: c.pricing?.tcgplayer
      ? { prices: { normal: mapTier(c.pricing.tcgplayer.normal), holofoil: mapTier(c.pricing.tcgplayer.holofoil), reverseHolofoil: mapTier(c.pricing.tcgplayer.reverseHolofoil) } }
      : undefined,
  }
}

export async function searchPokemonCards(
  filters: CardFilters = {},
  page = 1,
  pageSize = 20,
): Promise<PokemonCardPage> {
  const params = new URLSearchParams({ page: String(page), itemsPerPage: String(pageSize) })
  if (filters.name)   params.set('name', `${filters.name}*`)
  if (filters.type)   params.set('types', filters.type)
  if (filters.rarity) params.set('rarity', filters.rarity)
  if (filters.setId)  params.set('set.id', filters.setId)
  // TCGdex supports orderBy for name; price sorts fall back to name
  if (filters.sort === 'name') params.set('sort', 'name')

  const res = await fetch(`${BASE}/cards?${params}`)
  if (!res.ok) throw new Error(`TCGdex API error: ${res.status}`)
  const raw: TcgdexCard[] = await res.json()
  const data = raw.map(mapCard)

  // Client-side price sort (TCGdex doesn't support orderBy price)
  if (filters.sort === 'price-asc' || filters.sort === 'price-desc') {
    const dir = filters.sort === 'price-asc' ? 1 : -1
    data.sort((a, b) => {
      const pa = a.tcgplayer?.prices?.normal?.market ?? a.tcgplayer?.prices?.holofoil?.market ?? 0
      const pb = b.tcgplayer?.prices?.normal?.market ?? b.tcgplayer?.prices?.holofoil?.market ?? 0
      return (pa - pb) * dir
    })
  }

  return { data, totalCount: data.length < pageSize ? (page - 1) * pageSize + data.length : page * pageSize + pageSize, page, pageSize, count: data.length }
}

export async function fetchSets(): Promise<PokemonSet[]> {
  const res = await fetch(`${BASE}/sets`)
  if (!res.ok) throw new Error(`TCGdex API error: ${res.status}`)
  const raw: TcgdexSet[] = await res.json()
  return raw.map(s => ({
    id: s.id,
    name: s.name,
    series: s.serie?.name ?? '',
    releaseDate: s.releaseDate ?? '',
  }))
}

export async function fetchAutocompleteSuggestions(name: string): Promise<string[]> {
  if (name.length < 2) return []
  try {
    const params = new URLSearchParams({ name: `${name}*`, itemsPerPage: '10' })
    const res = await fetch(`${BASE}/cards?${params}`)
    if (!res.ok) return []
    const raw: TcgdexCard[] = await res.json()
    return [...new Set(raw.map(c => c.name))].slice(0, 5)
  } catch {
    return []
  }
}
