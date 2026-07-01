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
  it('defaults to the latest set when filters are empty', async () => {
    mockFetch.mockReturnValue(makeResponse([]))
    await searchPokemonCards({})
    const url = mockFetch.mock.calls[0][0] as string
    expect(url).not.toContain('name=')
    expect(url).toContain('set=sv10')
  })

  it('builds name filter correctly', async () => {
    mockFetch.mockReturnValue(makeResponse([]))
    await searchPokemonCards({ name: 'Pikachu' })
    const url = mockFetch.mock.calls[0][0] as string
    expect(url).toContain('name=Pikachu')
  })

  it('combines name + type + rarity filters', async () => {
    mockFetch.mockReturnValue(makeResponse([]))
    await searchPokemonCards({ name: 'Char', type: 'Fire', rarity: 'Rare' })
    const url = mockFetch.mock.calls[0][0] as string
    expect(url).toContain('name=Char')
    expect(url).toContain('types=Fire')
    expect(url).toContain('rarity=Rare')
  })

  it('sorts by set release date (newest first) when sets are provided', async () => {
    mockFetch.mockReturnValue(makeResponse([
      { id: 'old-001', localId: '001', name: 'Old Card', image: 'https://x/old' },
      { id: 'new-001', localId: '001', name: 'New Card', image: 'https://x/new' },
    ]))
    const sets = [
      { id: 'old', name: 'Old Set', series: '', releaseDate: '2020-01-01' },
      { id: 'new', name: 'New Set', series: '', releaseDate: '2024-01-01' },
    ]
    const result = await searchPokemonCards({ sort: 'newest' }, 1, 20, sets)
    expect(result.data.map(c => c.id)).toEqual(['new-001', 'old-001'])
  })

  it('throws on non-ok response', async () => {
    mockFetch.mockReturnValue(makeResponse({}, false))
    await expect(searchPokemonCards({})).rejects.toThrow('TCGdex API error: 429')
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
      { id: '1', localId: '1', name: 'Pikachu' }, { id: '2', localId: '2', name: 'Pikachu' },
      { id: '3', localId: '3', name: 'Piplup' }, { id: '4', localId: '4', name: 'Pidgey' },
      { id: '5', localId: '5', name: 'Pidgeot' }, { id: '6', localId: '6', name: 'Pidgeotto' },
    ]
    mockFetch.mockReturnValue(makeResponse(cards))
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
