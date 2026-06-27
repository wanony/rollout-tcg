# Search & Filtering

The Cards page provides a full-featured card browsing experience: debounced autocomplete search, filter chips, sort controls, and infinite scroll.

## CardFilters state

All active filters live in a single `CardFilters` object in `CardsPage`:

```typescript
interface CardFilters {
  q?: string        // search query
  type?: string     // e.g., "Fire"
  rarity?: string   // e.g., "Rare Holo"
  set?: string      // e.g., "Base Set"
  sort?: string     // "name", "price-asc", "price-desc", "rarity"
}
```

Filter changes reset the infinite scroll back to page 1 — TanStack Query's `queryKey` includes the full filter object so changing any filter triggers a fresh query.

## Infinite scroll

`useInfiniteQuery` from TanStack Query v5 appends pages as the user scrolls:

```typescript
const { data, fetchNextPage, hasNextPage } = useInfiniteQuery({
  queryKey: ['cards', filters],
  queryFn: ({ pageParam }) => searchCards({ ...filters, page: pageParam }),
  initialPageParam: 1,
  getNextPageParam: (lastPage) =>
    lastPage.results.length === PAGE_SIZE ? lastPage.page + 1 : undefined,
})
```

An `IntersectionObserver` on a sentinel `<div>` at the bottom of the grid triggers `fetchNextPage()` when the user scrolls near the end.

## AutocompleteDropdown

The search input debounces keystrokes by 300ms before firing a query, preventing a request on every keystroke:

```typescript
// Inside AutocompleteDropdown
const [inputValue, setInputValue] = useState('')
const debouncedValue = useDebounce(inputValue, 300)

const { data: suggestions } = useQuery({
  queryKey: ['autocomplete', debouncedValue],
  queryFn: () => searchCards({ q: debouncedValue, pageSize: 5 }),
  enabled: debouncedValue.length >= 2,
})
```

Suggestions are keyboard-navigable: `↑`/`↓` move the highlight, `Enter` selects, `Escape` closes without selecting and returns focus to the input.

## FilterChips

Active filters are shown as dismissible chips below the search bar. Clicking a chip removes that filter:

```typescript
// Type chips
['Fire', 'Water', 'Grass', 'Electric', 'Psychic', 'Fighting', 'Darkness', 'Metal', 'Dragon', 'Colorless']
```

Each active filter chip shows with a coloured ring matching its type glow colour (via `typeColors.ts`).

## SortDropdown

```typescript
type SortOption = 'name' | 'price-asc' | 'price-desc' | 'rarity'
```

Sort order is passed to the Card Catalog API's `sort` parameter. The API delegates sorting to MeiliSearch.

## Command palette integration

`CardsPage` registers contextual commands via `PageCommandsContext`:

| Command | Action |
|---|---|
| Search cards | Focus the search input |
| Clear filters | Reset all active filters |
| Sort: Name A–Z | Set sort to `name` |
| Sort: Price high–low | Set sort to `price-desc` |
