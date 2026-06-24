const BASE = 'https://api.pokemontcg.io/v2'

export interface PokemonCard {
  id: string
  name: string
  rarity: string
  set: { name: string; series: string }
  types?: string[]
  hp?: string
  images: { small: string; large: string }
  supertype: string
}

export interface PokemonCardPage {
  data: PokemonCard[]
  totalCount: number
  page: number
  pageSize: number
  count: number
}

export async function searchPokemonCards(
  q = '',
  page = 1,
  pageSize = 20,
): Promise<PokemonCardPage> {
  const params = new URLSearchParams({
    pageSize: String(pageSize),
    page: String(page),
    orderBy: '-set.releaseDate',
  })
  if (q) params.set('q', `name:${q}*`)
  const res = await fetch(`${BASE}/cards?${params}`)
  if (!res.ok) throw new Error(`Pokemon TCG API error: ${res.status}`)
  return res.json()
}
