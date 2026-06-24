# Search & Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the basic search input with debounced autocomplete and a filter chips row (type, rarity, set), add sort controls and result count, and convert the card grid to infinite scroll. Extend the Cmd+K command palette with contextual search actions.

**Architecture:** `CardFilters` is the single source of truth for all active filters in `CardsPage`. `useInfiniteQuery` replaces `useQuery` — pages are appended as the user scrolls; an `IntersectionObserver` on a sentinel div triggers `fetchNextPage`. Three new focused components (`AutocompleteDropdown`, `FilterChips`, `SortDropdown`) receive filters and an `onChange` callback. `CommandPalette` gains a `pageCommands` prop already wired from Plan A; `CardsPage` registers contextual commands via `PageCommandsContext`.

**Tech Stack:** React 18, TypeScript, Tailwind CSS, `@tanstack/react-query` v5 `useInfiniteQuery` (already installed), Vitest (already set up in Plan A)

## Global Constraints

- Execute after Plan A (Navigation & Transitions) AND Plan B (Card Visual System)
- `GlassSurface`, `CommandPalette`, `PageCommandsContext`, `PaletteCommand` already exist from Plan A
- `CardDetailModal`, `TYPE_GLOW`, `primaryGlow` already exist from Plan B
- `CardFilters` stub already exists in `pokemontcg.ts` from Plan B Task 6 — this plan replaces it with the full version
- Run all commands from `src/Frontend/`
- pokemontcg.io v2 base URL: `https://api.pokemontcg.io/v2`
- `useInfiniteQuery` v5 signature: `queryFn: ({ pageParam }) => ...`, `initialPageParam: 1`, `getNextPageParam: (lastPage) => ...`

---

### Task 1: Expand pokemontcg.ts API layer

**Files:**
- Modify: `src/Frontend/src/api/pokemontcg.ts`
- Create: `src/Frontend/src/api/pokemontcg.test.ts`

**Interfaces:**
- Produces: `export interface CardFilters { name?: string; type?: string; rarity?: string; setId?: string; sort?: 'newest' | 'price-asc' | 'price-desc' | 'name' }`
- Produces: `export interface PokemonSet { id: string; name: string; series: string; releaseDate: string }`
- Produces: `export async function searchPokemonCards(filters, page, pageSize): Promise<PokemonCardPage>`
- Produces: `export async function fetchSets(): Promise<PokemonSet[]>`
- Produces: `export async function fetchAutocompleteSuggestions(name: string): Promise<string[]>`

- [ ] **Step 1: Write the failing tests**

Create `src/Frontend/src/api/pokemontcg.test.ts`:

```ts
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
```

- [ ] **Step 2: Run to verify tests fail**

```bash
cd src/Frontend && npm test -- pokemontcg
```

Expected: Several FAIL — functions don't have the right signatures yet.

- [ ] **Step 3: Replace pokemontcg.ts with full implementation**

Replace the entire content of `src/Frontend/src/api/pokemontcg.ts`:

```ts
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

  const res = await fetch(`${BASE}/cards?${params}`)
  if (!res.ok) throw new Error(`Pokemon TCG API error: ${res.status}`)
  return res.json()
}

export async function fetchSets(): Promise<PokemonSet[]> {
  const res = await fetch(`${BASE}/sets?orderBy=-releaseDate&pageSize=250`)
  if (!res.ok) throw new Error(`Pokemon TCG API error: ${res.status}`)
  const data = await res.json()
  return data.data
}

export async function fetchAutocompleteSuggestions(name: string): Promise<string[]> {
  if (name.length < 2) return []
  const params = new URLSearchParams({ q: `name:${name}*`, pageSize: '6', select: 'name' })
  const res = await fetch(`${BASE}/cards?${params}`)
  if (!res.ok) return []
  const data: PokemonCardPage = await res.json()
  return [...new Set(data.data.map(c => c.name))].slice(0, 5)
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd src/Frontend && npm test -- pokemontcg
```

Expected: `7 passed`

- [ ] **Step 5: Verify TypeScript compiles**

```bash
cd src/Frontend && npx tsc --noEmit
```

Expected: No errors.

- [ ] **Step 6: Commit**

```bash
cd src/Frontend && git add src/api/pokemontcg.ts src/api/pokemontcg.test.ts && git commit -m "feat: full CardFilters API — searchPokemonCards, fetchSets, autocomplete"
```

---

### Task 2: Build AutocompleteDropdown

**Files:**
- Create: `src/Frontend/src/components/AutocompleteDropdown.tsx`

**Interfaces:**
- Consumes: `fetchAutocompleteSuggestions` from `../api/pokemontcg`
- Produces: `export default AutocompleteDropdown` — props: `{ query: string; onSelect: (name: string) => void; onClose: () => void }`

- [ ] **Step 1: Create AutocompleteDropdown.tsx**

Create `src/Frontend/src/components/AutocompleteDropdown.tsx`:

```tsx
import { useEffect, useRef, useState, KeyboardEvent } from 'react'
import { fetchAutocompleteSuggestions } from '../api/pokemontcg'

interface AutocompleteDropdownProps {
  query: string
  onSelect: (name: string) => void
  onClose: () => void
}

export default function AutocompleteDropdown({ query, onSelect, onClose }: AutocompleteDropdownProps) {
  const [suggestions, setSuggestions] = useState<string[]>([])
  const [activeIndex, setActiveIndex] = useState(-1)
  const listRef = useRef<HTMLUListElement>(null)

  useEffect(() => {
    if (query.length < 2) { setSuggestions([]); return }
    let cancelled = false
    fetchAutocompleteSuggestions(query).then(results => {
      if (!cancelled) { setSuggestions(results); setActiveIndex(-1) }
    })
    return () => { cancelled = true }
  }, [query])

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setActiveIndex(i => Math.min(i + 1, suggestions.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActiveIndex(i => Math.max(i - 1, -1))
    } else if (e.key === 'Enter' && activeIndex >= 0) {
      e.preventDefault()
      onSelect(suggestions[activeIndex])
    } else if (e.key === 'Escape') {
      onClose()
    }
  }

  if (suggestions.length === 0) return null

  return (
    <div
      className="absolute left-0 right-0 top-full z-30 mt-1"
      onKeyDown={handleKeyDown}
    >
      <ul
        ref={listRef}
        className="overflow-hidden rounded-xl border border-slate-700/60 bg-slate-900 shadow-xl"
        role="listbox"
      >
        {suggestions.map((name, i) => (
          <li
            key={name}
            role="option"
            aria-selected={i === activeIndex}
            className={`cursor-pointer px-4 py-2.5 text-sm transition-colors
              ${i === activeIndex
                ? 'bg-violet-500/20 text-violet-200'
                : 'text-slate-200 hover:bg-violet-500/10 hover:text-slate-100'}`}
            onMouseDown={(e) => { e.preventDefault(); onSelect(name) }}
            onMouseEnter={() => setActiveIndex(i)}
          >
            {name}
          </li>
        ))}
      </ul>
    </div>
  )
}
```

Note: `onMouseDown` uses `preventDefault()` to stop the input from blurring before the click registers.

- [ ] **Step 2: Commit**

```bash
cd src/Frontend && git add src/components/AutocompleteDropdown.tsx && git commit -m "feat: AutocompleteDropdown with keyboard navigation"
```

---

### Task 3: Build FilterChips

**Files:**
- Create: `src/Frontend/src/components/FilterChips.tsx`
- Create: `src/Frontend/src/components/FilterChips.test.tsx`

**Interfaces:**
- Consumes: `CardFilters, PokemonSet` from `../api/pokemontcg`
- Consumes: `TYPE_GLOW` from `../lib/typeColors`
- Produces: `export default FilterChips` — props: `{ filters: CardFilters; sets: PokemonSet[]; onChange: (f: CardFilters) => void }`

- [ ] **Step 1: Write the failing test**

Create `src/Frontend/src/components/FilterChips.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import FilterChips from './FilterChips'
import { CardFilters, PokemonSet } from '../api/pokemontcg'

const noSets: PokemonSet[] = []

describe('FilterChips', () => {
  it('renders type chips for all 11 types', () => {
    render(<FilterChips filters={{}} sets={noSets} onChange={vi.fn()} />)
    expect(screen.getByText('Fire')).toBeInTheDocument()
    expect(screen.getByText('Water')).toBeInTheDocument()
    expect(screen.getByText('Psychic')).toBeInTheDocument()
  })

  it('clicking a type chip calls onChange with that type', async () => {
    const onChange = vi.fn()
    render(<FilterChips filters={{}} sets={noSets} onChange={onChange} />)
    await userEvent.click(screen.getByText('Fire'))
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ type: 'Fire' }))
  })

  it('clicking an active type chip deselects it (calls onChange without type)', async () => {
    const onChange = vi.fn()
    const filters: CardFilters = { type: 'Fire' }
    render(<FilterChips filters={filters} sets={noSets} onChange={onChange} />)
    await userEvent.click(screen.getByText('Fire'))
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ type: undefined }))
  })

  it('clicking a rarity chip calls onChange with that rarity', async () => {
    const onChange = vi.fn()
    render(<FilterChips filters={{}} sets={noSets} onChange={onChange} />)
    await userEvent.click(screen.getByText('Ultra Rare'))
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ rarity: 'Ultra Rare' }))
  })

  it('shows Clear filters button when a filter is active', () => {
    render(<FilterChips filters={{ type: 'Fire' }} sets={noSets} onChange={vi.fn()} />)
    expect(screen.getByText('× Clear filters')).toBeInTheDocument()
  })

  it('does not show Clear filters when only sort is set', () => {
    render(<FilterChips filters={{ sort: 'newest' }} sets={noSets} onChange={vi.fn()} />)
    expect(screen.queryByText('× Clear filters')).toBeNull()
  })
})
```

- [ ] **Step 2: Run to verify it fails**

```bash
cd src/Frontend && npm test -- FilterChips
```

Expected: FAIL — `Cannot find module './FilterChips'`

- [ ] **Step 3: Create FilterChips.tsx**

Create `src/Frontend/src/components/FilterChips.tsx`:

```tsx
import { CardFilters, PokemonSet } from '../api/pokemontcg'
import { TYPE_GLOW } from '../lib/typeColors'

const TYPES = ['Fire','Water','Grass','Lightning','Psychic','Fighting','Darkness','Metal','Dragon','Fairy','Colorless']
const RARITIES = ['Common', 'Uncommon', 'Rare', 'Ultra Rare']

interface FilterChipsProps {
  filters: CardFilters
  sets: PokemonSet[]
  onChange: (filters: CardFilters) => void
}

export default function FilterChips({ filters, onChange, sets }: FilterChipsProps) {
  const hasActiveFilter = !!(filters.name || filters.type || filters.rarity || filters.setId)

  function setType(type: string) {
    onChange({ ...filters, type: filters.type === type ? undefined : type })
  }

  function setRarity(rarity: string) {
    onChange({ ...filters, rarity: filters.rarity === rarity ? undefined : rarity })
  }

  function clearAll() {
    onChange({ sort: filters.sort })
  }

  return (
    <div className="mb-4 space-y-2">
      {/* Type chips */}
      <div className="flex gap-1.5 overflow-x-auto pb-1">
        {TYPES.map(type => {
          const rgb = TYPE_GLOW[type] ?? '139 92 246'
          const active = filters.type === type
          return (
            <button
              key={type}
              onClick={() => setType(type)}
              className="flex-shrink-0 rounded-full px-2.5 py-1 text-xs font-medium transition-all"
              style={active
                ? { background: `rgb(${rgb} / 0.2)`, border: `1px solid rgb(${rgb} / 0.5)`, color: `rgb(${rgb})` }
                : { background: 'rgb(30 41 59 / 0.6)', border: '1px solid rgb(71 85 105 / 0.4)', color: '#94a3b8' }}
            >
              {type}
            </button>
          )
        })}
      </div>

      {/* Rarity chips + Set dropdown + Clear */}
      <div className="flex flex-wrap items-center gap-2">
        {RARITIES.map(rarity => {
          const active = filters.rarity === rarity
          return (
            <button
              key={rarity}
              onClick={() => setRarity(rarity)}
              className={`rounded-full px-2.5 py-1 text-xs font-medium transition-all
                ${active
                  ? 'bg-violet-500/20 border border-violet-500/50 text-violet-300'
                  : 'bg-slate-800/60 border border-slate-700/40 text-slate-400 hover:text-slate-200'}`}
            >
              {rarity}
            </button>
          )
        })}

        {sets.length > 0 && (
          <select
            value={filters.setId ?? ''}
            onChange={e => onChange({ ...filters, setId: e.target.value || undefined })}
            className="rounded-lg border border-slate-700/60 bg-slate-900 px-3 py-1 text-xs text-slate-200 focus:outline-none focus:border-violet-500"
          >
            <option value="">All sets</option>
            {sets.map(s => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        )}

        {hasActiveFilter && (
          <button
            onClick={clearAll}
            className="text-xs text-slate-500 hover:text-slate-300 transition-colors"
          >
            × Clear filters
          </button>
        )}
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd src/Frontend && npm test -- FilterChips
```

Expected: `6 passed`

- [ ] **Step 5: Commit**

```bash
cd src/Frontend && git add src/components/FilterChips.tsx src/components/FilterChips.test.tsx && git commit -m "feat: FilterChips with type/rarity/set filters"
```

---

### Task 4: Build SortDropdown

**Files:**
- Create: `src/Frontend/src/components/SortDropdown.tsx`

**Interfaces:**
- Consumes: `CardFilters` from `../api/pokemontcg`
- Produces: `export default SortDropdown` — props: `{ filters: CardFilters; totalCount: number; onChange: (f: CardFilters) => void }`

- [ ] **Step 1: Create SortDropdown.tsx**

Create `src/Frontend/src/components/SortDropdown.tsx`:

```tsx
import { CardFilters } from '../api/pokemontcg'

interface SortDropdownProps {
  filters: CardFilters
  totalCount: number
  onChange: (filters: CardFilters) => void
}

export default function SortDropdown({ filters, totalCount, onChange }: SortDropdownProps) {
  return (
    <div className="mb-3 flex items-center justify-between">
      <span className="text-sm text-slate-500">
        {totalCount > 0 ? `${totalCount.toLocaleString()} cards` : ''}
      </span>
      <select
        value={filters.sort ?? 'newest'}
        onChange={e => onChange({ ...filters, sort: e.target.value as CardFilters['sort'] })}
        className="rounded-lg border border-slate-700/60 bg-slate-900 px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-violet-500"
      >
        <option value="newest">Newest</option>
        <option value="price-desc">Price: High → Low</option>
        <option value="price-asc">Price: Low → High</option>
        <option value="name">Name A → Z</option>
      </select>
    </div>
  )
}
```

- [ ] **Step 2: Commit**

```bash
cd src/Frontend && git add src/components/SortDropdown.tsx && git commit -m "feat: SortDropdown component"
```

---

### Task 5: Rework CardsPage with infinite scroll and full filter state

**Files:**
- Modify: `src/Frontend/src/pages/CardsPage.tsx`

**Interfaces:**
- Consumes: `useInfiniteQuery` from `@tanstack/react-query`
- Consumes: `FilterChips` from `../components/FilterChips`
- Consumes: `SortDropdown` from `../components/SortDropdown`
- Consumes: `AutocompleteDropdown` from `../components/AutocompleteDropdown`
- Consumes: `fetchSets, searchPokemonCards, CardFilters, PokemonCard` from `../api/pokemontcg`
- Consumes: `PageCommandsContext, PaletteCommand` from `../components/CommandPalette`

- [ ] **Step 1: Replace CardsPage.tsx**

Replace the full content of `src/Frontend/src/pages/CardsPage.tsx`:

```tsx
import { useState, useEffect, useRef, useContext, useCallback } from 'react'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { motion, AnimatePresence } from 'framer-motion'
import { searchPokemonCards, fetchSets, CardFilters, PokemonCard } from '../api/pokemontcg'
import { TYPE_GLOW } from '../lib/typeColors'
import HoloCard from '../components/HoloCard'
import CardDetailModal from '../components/CardDetailModal'
import FilterChips from '../components/FilterChips'
import SortDropdown from '../components/SortDropdown'
import AutocompleteDropdown from '../components/AutocompleteDropdown'
import { PageCommandsContext } from '../components/CommandPalette'

function TypeBadge({ type }: { type: string }) {
  const rgb = TYPE_GLOW[type] ?? '139 92 246'
  return (
    <span
      className="inline-flex rounded-full px-1.5 py-0.5 text-[9px] font-medium leading-none"
      style={{
        background: `rgb(${rgb} / 0.2)`,
        border: `1px solid rgb(${rgb} / 0.35)`,
        color: `rgb(${rgb})`,
      }}
    >
      {type}
    </span>
  )
}

function marketPrice(card: PokemonCard): string | null {
  const p = card.tcgplayer?.prices
  const val = p?.holofoil?.market ?? p?.normal?.market ?? p?.reverseHolofoil?.market
  return val != null ? `$${val.toFixed(2)}` : null
}

export default function CardsPage() {
  const searchInputRef = useRef<HTMLInputElement>(null)
  const sentinelRef = useRef<HTMLDivElement>(null)
  const setPageCommands = useContext(PageCommandsContext)

  const [nameInput, setNameInput] = useState('')
  const [showSuggestions, setShowSuggestions] = useState(false)
  const [filters, setFilters] = useState<CardFilters>({ sort: 'newest' })
  const [selectedCard, setSelectedCard] = useState<PokemonCard | null>(null)

  // Debounce nameInput → filters.name
  useEffect(() => {
    const t = setTimeout(() => {
      setFilters(f => ({ ...f, name: nameInput || undefined }))
    }, 300)
    return () => clearTimeout(t)
  }, [nameInput])

  // Register contextual commands in the palette
  useEffect(() => {
    const commands = [
      {
        id: 'filter-fire',
        label: 'Filter: Fire type',
        group: 'Search',
        action: () => setFilters(f => ({ ...f, type: 'Fire' })),
      },
      {
        id: 'filter-ultra',
        label: 'Filter: Ultra Rare',
        group: 'Search',
        action: () => setFilters(f => ({ ...f, rarity: 'Ultra Rare' })),
      },
      {
        id: 'filter-clear',
        label: 'Clear all filters',
        group: 'Search',
        action: () => setFilters(f => ({ sort: f.sort })),
      },
      {
        id: 'sort-price',
        label: 'Sort by price (high → low)',
        group: 'Search',
        action: () => setFilters(f => ({ ...f, sort: 'price-desc' })),
      },
    ]
    setPageCommands(commands)
    return () => setPageCommands([])
  }, [setPageCommands])

  const { data: sets = [] } = useQuery({
    queryKey: ['pokemon-sets'],
    queryFn: fetchSets,
    staleTime: 24 * 60 * 60 * 1000,
  })

  const { data, isLoading, isFetchingNextPage, fetchNextPage, hasNextPage } = useInfiniteQuery({
    queryKey: ['pokemon-cards', filters],
    queryFn: ({ pageParam }) => searchPokemonCards(filters, pageParam as number, 20),
    initialPageParam: 1,
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
    staleTime: 5 * 60 * 1000,
  })

  const cards = data?.pages.flatMap(p => p.data) ?? []
  const totalCount = data?.pages[0]?.totalCount ?? 0

  // Infinite scroll sentinel
  useEffect(() => {
    const el = sentinelRef.current
    if (!el) return
    const obs = new IntersectionObserver(
      ([entry]) => { if (entry.isIntersecting && hasNextPage && !isFetchingNextPage) fetchNextPage() },
      { threshold: 0.1 },
    )
    obs.observe(el)
    return () => obs.disconnect()
  }, [hasNextPage, isFetchingNextPage, fetchNextPage])

  const handleSelectSuggestion = useCallback((name: string) => {
    setNameInput(name)
    setFilters(f => ({ ...f, name }))
    setShowSuggestions(false)
  }, [])

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-slate-100 sm:text-2xl">Card Catalog</h1>

      {/* Search + autocomplete */}
      <div className="relative mb-3 flex gap-2">
        <div className="relative flex-1">
          <input
            ref={searchInputRef}
            value={nameInput}
            onChange={e => { setNameInput(e.target.value); setShowSuggestions(true) }}
            onFocus={() => setShowSuggestions(true)}
            onBlur={() => setTimeout(() => setShowSuggestions(false), 150)}
            onKeyDown={e => {
              if (e.key === 'Enter') { setFilters(f => ({ ...f, name: nameInput || undefined })); setShowSuggestions(false) }
            }}
            placeholder="Search Pokémon cards…"
            className="w-full rounded-lg border border-slate-700/60 bg-slate-900/80 px-3 py-2.5 text-sm text-slate-100 placeholder-slate-500 backdrop-blur-sm focus:border-violet-500 focus:outline-none"
          />
          {showSuggestions && (
            <AutocompleteDropdown
              query={nameInput}
              onSelect={handleSelectSuggestion}
              onClose={() => setShowSuggestions(false)}
            />
          )}
        </div>
        <button
          onClick={() => { setFilters(f => ({ ...f, name: nameInput || undefined })); setShowSuggestions(false) }}
          className="rounded-lg bg-violet-600 px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-violet-500 active:scale-95"
        >
          Search
        </button>
      </div>

      {/* Filters */}
      <FilterChips filters={filters} sets={sets} onChange={setFilters} />

      {/* Sort + count */}
      <SortDropdown filters={filters} totalCount={totalCount} onChange={setFilters} />

      {/* Card grid */}
      {isLoading && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {Array.from({ length: 10 }).map((_, i) => (
            <div key={i} className="animate-pulse rounded-[4.5%/3.5%] bg-slate-800/60" style={{ aspectRatio: '5/7' }} />
          ))}
        </div>
      )}

      {!isLoading && cards.length === 0 && (
        <div className="mt-16 text-center text-slate-500">
          No cards match these filters.{' '}
          {(filters.type || filters.rarity || filters.setId || filters.name) && (
            <button
              onClick={() => { setFilters({ sort: filters.sort }); setNameInput('') }}
              className="text-violet-400 hover:text-violet-300 underline"
            >
              Clear filters
            </button>
          )}
        </div>
      )}

      {cards.length > 0 && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {cards.map((card, i) => {
            const price = marketPrice(card)
            return (
              <motion.div
                key={`${card.id}-${i}`}
                initial={{ opacity: 0, scale: 0.92 }}
                animate={{ opacity: 1, scale: 1 }}
                transition={{ delay: Math.min((i % 20) * 0.04, 0.5), duration: 0.25 }}
                style={{ aspectRatio: '5/7' }}
                onClick={() => setSelectedCard(card)}
                className="cursor-pointer"
              >
                <HoloCard rarity={card.rarity} types={card.types} className="h-full w-full">
                  <div className="relative h-full w-full overflow-hidden rounded-[4.5%/3.5%]">
                    <img
                      src={card.images.small}
                      alt={card.name}
                      className="h-full w-full object-cover"
                      loading="lazy"
                      draggable={false}
                    />
                    <div className="absolute bottom-0 left-0 right-0 bg-gradient-to-t from-black/80 to-transparent px-2 pb-1.5 pt-4">
                      <div className="truncate text-xs font-semibold text-white">{card.name}</div>
                      <div className="flex items-center justify-between gap-1">
                        <div className="truncate text-[10px] text-slate-300 opacity-80">{card.set.name}</div>
                        {price && (
                          <span className="flex-shrink-0 text-[10px] font-semibold text-emerald-400">{price}</span>
                        )}
                      </div>
                      {card.types && card.types.length > 0 && (
                        <div className="mt-0.5 flex flex-wrap gap-0.5">
                          {card.types.map(t => <TypeBadge key={t} type={t} />)}
                        </div>
                      )}
                    </div>
                  </div>
                </HoloCard>
              </motion.div>
            )
          })}
        </div>
      )}

      {/* Infinite scroll sentinel */}
      <div ref={sentinelRef} className="h-8" />

      {/* Loading more indicator */}
      {isFetchingNextPage && (
        <div className="grid grid-cols-2 gap-4 py-4 sm:grid-cols-3 lg:grid-cols-5">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="animate-pulse rounded-[4.5%/3.5%] bg-slate-800/60" style={{ aspectRatio: '5/7' }} />
          ))}
        </div>
      )}

      <AnimatePresence>
        {selectedCard && (
          <CardDetailModal
            card={selectedCard}
            onClose={() => setSelectedCard(null)}
          />
        )}
      </AnimatePresence>
    </div>
  )
}
```

- [ ] **Step 2: Verify TypeScript compiles**

```bash
cd src/Frontend && npx tsc --noEmit
```

Expected: No errors.

- [ ] **Step 3: Verify in browser**

```bash
cd src/Frontend && npm run dev
```

Navigate to `/cards`. Expected:
- Type chips row shows all 11 types; clicking one filters the grid
- Rarity chips filter by rarity; Set dropdown populates from pokemontcg.io `/v2/sets`
- Typing in search input shows autocomplete dropdown after 2+ characters; clicking a suggestion searches immediately
- Sort dropdown changes order; result count shows above the grid
- Scrolling to the bottom of the grid triggers loading of next page (skeleton row appears, then new cards append)
- `⌘K` opens the command palette with a "Search" group showing Filter / Sort / Clear commands

- [ ] **Step 4: Run all tests**

```bash
cd src/Frontend && npm test
```

Expected: All tests pass (typeColors: 7, CommandPalette: 3, pokemontcg: 7, FilterChips: 6 — 23 total).

- [ ] **Step 5: Commit**

```bash
cd src/Frontend && git add src/pages/CardsPage.tsx && git commit -m "feat: infinite scroll + autocomplete + filter chips + sort on CardsPage"
```
