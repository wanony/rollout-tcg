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

// TCGdex shapes
interface TcgdexListCard {
  id: string
  name: string
  image?: string
  localId: string
}

interface TcgdexPriceTier {
  lowPrice?: number
  midPrice?: number
  highPrice?: number
  marketPrice?: number
}

interface TcgdexFullCard extends TcgdexListCard {
  category: string
  rarity?: string
  types?: string[]
  hp?: string
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

function mapListCard(c: TcgdexListCard): PokemonCard {
  return {
    id: c.id,
    name: c.name,
    rarity: 'Unknown',
    set: { id: '', name: '', series: '' },
    types: undefined,
    hp: undefined,
    images: {
      small: c.image ? `${c.image}/low.webp` : '',
      large: c.image ? `${c.image}/high.webp` : '',
    },
    supertype: 'Pokemon',
    tcgplayer: undefined,
  }
}

function mapFullCard(c: TcgdexFullCard): PokemonCard {
  return {
    id: c.id,
    name: c.name,
    rarity: c.rarity ?? 'Unknown',
    set: { id: c.set.id, name: c.set.name, series: c.set.serie?.name ?? '' },
    types: c.types,
    hp: c.hp != null ? String(c.hp) : undefined,
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

// TCGdex returns all matching results with no server-side pagination — we slice client-side
async function fetchFiltered(params: URLSearchParams): Promise<TcgdexListCard[]> {
  const res = await fetch(`${BASE}/cards?${params}`)
  if (!res.ok) throw new Error(`TCGdex API error: ${res.status}`)
  return res.json()
}

export async function searchPokemonCards(
  filters: CardFilters = {},
  page = 1,
  pageSize = 20,
): Promise<PokemonCardPage> {
  const params = new URLSearchParams()
  const hasFilter = !!(filters.name || filters.type || filters.rarity || filters.setId)
  if (filters.name)   params.set('name', filters.name)
  if (filters.type)   params.set('types', filters.type)
  if (filters.rarity) params.set('rarity', filters.rarity)
  if (filters.setId)  params.set('set', filters.setId)
  if (!hasFilter)     params.set('name', 'Pikachu')

  const all = (await fetchFiltered(params)).filter(c => c.image)

  // Client-side sort
  if (filters.sort === 'name') all.sort((a, b) => a.name.localeCompare(b.name))

  const start = (page - 1) * pageSize
  const slice = all.slice(start, start + pageSize)

  return {
    data: slice.map(mapListCard),
    totalCount: all.length,
    page,
    pageSize,
    count: slice.length,
  }
}

// Called by CardDetailModal to get full card data including types, rarity, pricing
export async function fetchCardById(id: string): Promise<PokemonCard> {
  const res = await fetch(`${BASE}/cards/${encodeURIComponent(id)}`)
  if (!res.ok) throw new Error(`TCGdex API error: ${res.status}`)
  const raw: TcgdexFullCard = await res.json()
  return mapFullCard(raw)
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
    const res = await fetch(`${BASE}/cards?name=${encodeURIComponent(name)}`)
    if (!res.ok) return []
    const raw: TcgdexListCard[] = await res.json()
    return [...new Set(raw.map(c => c.name))].slice(0, 5)
  } catch {
    return []
  }
}
