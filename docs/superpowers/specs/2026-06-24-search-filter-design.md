# Search & Filter — Design Spec
**Date:** 2026-06-24  
**Status:** Approved

## Overview

Replace the basic Enter-to-search input with debounced autocomplete, a filter chips row (type/rarity/set), sort controls, and infinite scroll. TCGPlayer market prices are surfaced from the existing pokemontcg.io response. The Cmd+K command palette from sub-project A is extended with search-scoped commands.

## Architecture

### New files
- `src/components/FilterChips.tsx` — type, rarity, set filter UI
- `src/components/SortDropdown.tsx` — sort order control
- `src/components/AutocompleteDropdown.tsx` — name suggestion dropdown

### Modified files
- `src/pages/CardsPage.tsx` — useInfiniteQuery, filter/sort state, layout
- `src/api/pokemontcg.ts` — extend search params, add `fetchSets`, extend `PokemonCard` type
- `src/components/CommandPalette.tsx` — add search-scoped commands (sub-project A)

### Modified dependencies
- `@tanstack/react-query` — switch from `useQuery` to `useInfiniteQuery` (already installed)

## API Layer (`src/api/pokemontcg.ts`)

### Extended `PokemonCard` type
Add `tcgplayer` field (already specified in sub-project B spec — shared type, one source of truth).

### `fetchSets()`
```ts
export interface PokemonSet {
  id: string
  name: string
  series: string
  releaseDate: string
}

export async function fetchSets(): Promise<PokemonSet[]> {
  const res = await fetch(`${BASE}/sets?orderBy=-releaseDate&pageSize=250`)
  if (!res.ok) throw new Error(`Pokemon TCG API error: ${res.status}`)
  const data = await res.json()
  return data.data
}
```

Cached with `staleTime: 24 * 60 * 60 * 1000` (sets don't change often).

### Updated `searchPokemonCards()`
```ts
export interface CardFilters {
  name?: string
  type?: string
  rarity?: string
  setId?: string
  sort?: 'newest' | 'price-asc' | 'price-desc' | 'name'
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

  const ORDER: Record<string, string> = {
    newest:     '-set.releaseDate',
    'price-asc':  'cardmarket.prices.averageSellPrice',
    'price-desc': '-cardmarket.prices.averageSellPrice',
    name:       'name',
  }

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
```

Old signature `searchPokemonCards(q, page, pageSize)` is replaced — callers updated accordingly (only `CardsPage.tsx` calls this).

### `fetchAutocompleteSuggestions()`
```ts
export async function fetchAutocompleteSuggestions(name: string): Promise<string[]> {
  if (name.length < 2) return []
  const params = new URLSearchParams({ q: `name:${name}*`, pageSize: '6', select: 'name' })
  const res = await fetch(`${BASE}/cards?${params}`)
  if (!res.ok) return []
  const data: PokemonCardPage = await res.json()
  // Deduplicate names (same Pokémon in multiple sets)
  return [...new Set(data.data.map(c => c.name))].slice(0, 5)
}
```

## CardsPage

### State
```ts
const [nameInput, setNameInput]     = useState('')        // controlled input value
const [filters, setFilters]         = useState<CardFilters>({ sort: 'newest' })
const [showSuggestions, setShowSuggestions] = useState(false)
const [selectedCard, setSelectedCard] = useState<PokemonCard | null>(null)
```

`filters` is the single source of truth for the active query. `nameInput` is the live text; it debounces into `filters.name` after 300ms (via `useEffect` + `clearTimeout`).

### Infinite query
```ts
const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading } =
  useInfiniteQuery({
    queryKey: ['pokemon-cards', filters],
    queryFn: ({ pageParam = 1 }) => searchPokemonCards(filters, pageParam),
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
    staleTime: 5 * 60 * 1000,
  })

const cards = data?.pages.flatMap(p => p.data) ?? []
```

### Infinite scroll sentinel
```tsx
const sentinelRef = useRef<HTMLDivElement>(null)

useEffect(() => {
  if (!sentinelRef.current) return
  const obs = new IntersectionObserver(
    ([entry]) => { if (entry.isIntersecting && hasNextPage) fetchNextPage() },
    { threshold: 0.1 }
  )
  obs.observe(sentinelRef.current)
  return () => obs.disconnect()
}, [hasNextPage, fetchNextPage])

// In JSX, below the card grid:
<div ref={sentinelRef} className="h-8" />
```

### Layout structure
```
<SearchBar + AutocompleteDropdown>
<FilterChips>
<SortDropdown + result count>
<Card grid (motion.div items, same animation as today)>
<Sentinel div>
<Loading skeleton row (isFetchingNextPage)>
<CardDetailModal (portal, conditional on selectedCard)>
```

## AutocompleteDropdown (`src/components/AutocompleteDropdown.tsx`)

- Positioned absolutely below the search input
- Shows on focus + `nameInput.length >= 2`
- Items: card name suggestions from `fetchAutocompleteSuggestions`
- Click suggestion → sets `filters.name`, closes dropdown, fetches
- Keyboard: `↓`/`↑` arrow navigation, `Enter` selects, `Escape` closes
- Styled: `bg-slate-900 border border-slate-700/60 rounded-xl shadow-xl`
- Each item: `px-4 py-2.5 text-sm text-slate-200 hover:bg-violet-500/15`
- Debounced with the same 300ms as the main search (shares the `nameInput` dep)

## FilterChips (`src/components/FilterChips.tsx`)

### Props
```ts
interface FilterChipsProps {
  filters: CardFilters
  sets: PokemonSet[]
  onChange: (filters: CardFilters) => void
}
```

### Type chips
Row of pill buttons, one per Pokémon type. Background: `rgb(TYPE_GLOW[type] / 0.15)`, border: `rgb(TYPE_GLOW[type] / 0.4)` when active. Inactive: `bg-slate-800/60 border-slate-700/40`. Scrollable horizontally on mobile (`overflow-x-auto`). Multi-select: clicking an active type deselects it. Only one type at a time (pokemontcg.io `types:` param is single-value in practice for card filtering).

### Rarity chips
Single-select: Common · Uncommon · Rare · Ultra Rare. Same pill style without colour coding. 

### Set dropdown
`<select>` styled to match the dark theme (`bg-slate-900 border-slate-700/60 text-slate-200 rounded-lg px-3 py-1.5`). Options populated from `fetchSets()`. Value: `setId`. On change: updates `filters.setId`.

### Clear all
Appears as a small `×  Clear filters` link when any filter other than `sort` is active. Resets `filters` to `{ sort: filters.sort }`.

## SortDropdown (`src/components/SortDropdown.tsx`)

```tsx
<select value={filters.sort} onChange={e => onChange({ ...filters, sort: e.target.value })}>
  <option value="newest">Newest</option>
  <option value="price-desc">Price: High → Low</option>
  <option value="price-asc">Price: Low → High</option>
  <option value="name">Name A → Z</option>
</select>
```

Positioned right-aligned above the grid alongside a result count: `{data?.pages[0]?.totalCount ?? 0} cards`.

## CommandPalette Extension

In `CommandPalette.tsx`, add a search group visible when on `/cards`:

| Command | Action |
|---|---|
| Filter: Fire type | Sets `filters.type = 'Fire'`, closes palette |
| Filter: Ultra Rare | Sets `filters.rarity = 'Ultra Rare'`, closes palette |
| Clear filters | Resets filters |
| Sort by price | Sets `filters.sort = 'price-desc'` |

These commands are passed as callbacks from `CardsPage.tsx` into `CommandPalette` via context or props. Palette accepts `pageCommands?: Command[]` prop — empty by default, populated by pages that want to register contextual commands.

## Error Handling & Edge Cases
- pokemontcg.io rate limit (429): `useInfiniteQuery` retry with exponential backoff (react-query default)
- `fetchSets` failure: set dropdown shows "All sets" option only, filter omitted from query
- Autocomplete fetch error: silently suppressed — dropdown just doesn't appear
- Empty results: same "No cards found." message as today, centred below the filter row
- Filter combination yields 0 results: show "No cards match these filters." with a "Clear filters" link inline
- Infinite scroll at end: `hasNextPage` is false, sentinel stops triggering — no duplicate fetches

## Testing
```ts
// searchPokemonCards: builds correct q param from filters
// fetchAutocompleteSuggestions: deduplicates names, returns max 5
// FilterChips: clicking active type deselects it (toggles off)
// Infinite scroll: fetchNextPage called when sentinel intersects
```
