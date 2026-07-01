const BASE = 'https://api.tcgdex.net/v2/en'

interface PriceTier {
  low: number
  mid: number
  high: number
  market: number
}

export interface CardmarketPricing {
  unit: string
  avg?: number
  low?: number
  trend?: number
  avg1?: number
  avg7?: number
  avg30?: number
}

export interface PokemonCard {
  id: string
  name: string
  illustrator?: string
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
  cardmarket?: CardmarketPricing
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
  illustrator?: string
  sort?: 'newest' | 'name'
}

// id format is "<setId>-<localId>" (e.g. "sv10.5b-001") — setId may itself contain dashes,
// so drop only the last segment rather than splitting on the first dash.
function setIdFromCardId(cardId: string): string {
  return cardId.slice(0, cardId.lastIndexOf('-'))
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

interface TcgdexPricing {
  tcgplayer?: {
    unit?: string
    normal?: TcgdexPriceTier
    holofoil?: TcgdexPriceTier
    'reverse-holofoil'?: TcgdexPriceTier
  }
  cardmarket?: {
    unit?: string
    avg?: number
    low?: number
    trend?: number
    avg1?: number
    avg7?: number
    avg30?: number
  }
}

interface TcgdexFullCard extends TcgdexListCard {
  category: string
  illustrator?: string
  rarity?: string
  types?: string[]
  hp?: string | number
  set: { id: string; name: string; serie?: { name: string } }
  pricing?: TcgdexPricing
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
    cardmarket: undefined,
  }
}

function mapFullCard(c: TcgdexFullCard): PokemonCard {
  const tcp = c.pricing?.tcgplayer
  const cm = c.pricing?.cardmarket
  return {
    id: c.id,
    name: c.name,
    illustrator: c.illustrator,
    rarity: c.rarity ?? 'Unknown',
    set: { id: c.set.id, name: c.set.name, series: c.set.serie?.name ?? '' },
    types: c.types,
    hp: c.hp != null ? String(c.hp) : undefined,
    images: {
      small: c.image ? `${c.image}/low.webp` : '',
      large: c.image ? `${c.image}/high.webp` : '',
    },
    supertype: c.category,
    tcgplayer: tcp ? {
      prices: {
        normal: mapTier(tcp.normal),
        holofoil: mapTier(tcp.holofoil),
        reverseHolofoil: mapTier(tcp['reverse-holofoil']),
      },
    } : undefined,
    cardmarket: cm ? {
      unit: cm.unit ?? 'EUR',
      avg: cm.avg,
      low: cm.low,
      trend: cm.trend,
      avg1: cm.avg1,
      avg7: cm.avg7,
      avg30: cm.avg30,
    } : undefined,
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
  sets: PokemonSet[] = [],
): Promise<PokemonCardPage> {
  const params = new URLSearchParams()
  const hasFilter = !!(filters.name || filters.type || filters.rarity || filters.setId || filters.illustrator)
  if (filters.name)        params.set('name', filters.name)
  if (filters.type)        params.set('types', filters.type)
  if (filters.rarity)      params.set('rarity', filters.rarity)
  if (filters.setId)       params.set('set', filters.setId)
  if (filters.illustrator) params.set('illustrator', filters.illustrator)
  if (!hasFilter)          params.set('set', 'sv10')

  const all = (await fetchFiltered(params)).filter(c => c.image)

  // Client-side sort
  if (filters.sort === 'name') {
    all.sort((a, b) => a.name.localeCompare(b.name))
  } else {
    // "newest": rank by the card's set release date, newest set first, then by card number within a set
    const releaseDateBySet = new Map(sets.map(s => [s.id, s.releaseDate]))
    all.sort((a, b) => {
      const dateA = releaseDateBySet.get(setIdFromCardId(a.id)) ?? ''
      const dateB = releaseDateBySet.get(setIdFromCardId(b.id)) ?? ''
      if (dateA !== dateB) return dateB.localeCompare(dateA)
      return b.localId.localeCompare(a.localId, undefined, { numeric: true })
    })
  }

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
