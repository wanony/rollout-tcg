import { describe, it, expect, vi, beforeEach } from 'vitest'

// Mock global fetch
const mockFetch = vi.fn()
vi.stubGlobal('fetch', mockFetch)

beforeEach(() => mockFetch.mockReset())

// Import after stubbing
import { searchPokemonCards, fetchAutocompleteSuggestions } from './pokemontcg'

function makeResponse(data: unknown, ok = true) {
  return Promise.resolve({
    ok,
    status: ok ? 200 : 429,
    json: () => Promise.resolve(data),
  } as Response)
}

describe('searchPokemonCards', () => {
  it('sends no q param when filters are empty', async () => {
    mockFetch.mockReturnValue(makeResponse({ data: [], totalCount: 0, page: 1, pageSize: 20, count: 0 }))
    await searchPokemonCards({})
    const url = mockFetch.mock.calls[0][0] as string
    expect(url).not.toContain('&q=')
    expect(url).toContain('orderBy=-set.releaseDate')
  })

  it('builds name filter correctly', async () => {
    mockFetch.mockReturnValue(makeResponse({ data: [], totalCount: 0, page: 1, pageSize: 20, count: 0 }))
    await searchPokemonCards({ name: 'Pikachu' })
    const url = mockFetch.mock.calls[0][0] as string
    expect(url).toContain('name%3APikachu*')
  })

  it('combines name + type + rarity filters', async () => {
    mockFetch.mockReturnValue(makeResponse({ data: [], totalCount: 0, page: 1, pageSize: 20, count: 0 }))
    await searchPokemonCards({ name: 'Char', type: 'Fire', rarity: 'Rare' })
    const url = mockFetch.mock.calls[0][0] as string
    expect(url).toContain('name%3AChar*')
    expect(url).toContain('types%3AFire')
    expect(url).toContain('rarity%3A%22Rare%22')
  })

  it('uses price-desc orderBy for price-desc sort', async () => {
    mockFetch.mockReturnValue(makeResponse({ data: [], totalCount: 0, page: 1, pageSize: 20, count: 0 }))
    await searchPokemonCards({ sort: 'price-desc' })
    const url = mockFetch.mock.calls[0][0] as string
    expect(url).toContain('orderBy=-cardmarket.prices.averageSellPrice')
  })

  it('throws on non-ok response', async () => {
    mockFetch.mockReturnValue(makeResponse({}, false))
    await expect(searchPokemonCards({})).rejects.toThrow('Pokemon TCG API error: 429')
  })
})

describe('fetchAutocompleteSuggestions', () => {
  it('returns empty array for short input', async () => {
    const result = await fetchAutocompleteSuggestions('P')
    expect(result).toEqual([])
    expect(mockFetch).not.toHaveBeenCalled()
  })

  it('deduplicates names and returns max 5', async () => {
    const cards = [
      { name: 'Pikachu' }, { name: 'Pikachu' }, { name: 'Piplup' },
      { name: 'Pidgey' }, { name: 'Pidgeot' }, { name: 'Pidgeotto' },
    ]
    mockFetch.mockReturnValue(makeResponse({ data: cards, totalCount: 6, page: 1, pageSize: 6, count: 6 }))
    const result = await fetchAutocompleteSuggestions('Pi')
    expect(result).toHaveLength(5)
    expect(result.filter(n => n === 'Pikachu')).toHaveLength(1)
  })

  it('returns empty array on fetch error', async () => {
    mockFetch.mockReturnValue(makeResponse({}, false))
    const result = await fetchAutocompleteSuggestions('Pi')
    expect(result).toEqual([])
  })
})
