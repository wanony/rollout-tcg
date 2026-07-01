import { useState, useEffect, useRef, useContext, useCallback } from 'react'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { AnimatePresence } from 'framer-motion'
import { searchPokemonCards, fetchSets, CardFilters, PokemonCard } from '../api/pokemontcg'
import { TYPE_GLOW } from '../lib/typeColors'
import CardTile from '../components/CardTile'
import CardDetailModal from '../components/CardDetailModal'
import FilterChips from '../components/FilterChips'
import SortDropdown from '../components/SortDropdown'
import AutocompleteDropdown, { AutocompleteDropdownHandle } from '../components/AutocompleteDropdown'
import { PageCommandsContext } from '../components/CommandPalette'
import { useDitherOverride, typeGlowToDither } from '../components/DitherColorContext'

export default function CardsPage() {
  const searchInputRef = useRef<HTMLInputElement>(null)
  const autocompleteRef = useRef<AutocompleteDropdownHandle>(null)
  const sentinelRef = useRef<HTMLDivElement>(null)
  const setPageCommands = useContext(PageCommandsContext)

  const [nameInput, setNameInput] = useState('')
  const [showSuggestions, setShowSuggestions] = useState(false)
  const [filters, setFilters] = useState<CardFilters>({ sort: 'newest' })
  const setDitherOverride = useDitherOverride()

  useEffect(() => {
    const rgb = filters.type ? TYPE_GLOW[filters.type] : null
    setDitherOverride(rgb ? typeGlowToDither(rgb) : null)
    return () => setDitherOverride(null)
  }, [filters.type, setDitherOverride])
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
        id: 'sort-name',
        label: 'Sort by name (A → Z)',
        group: 'Search',
        action: () => setFilters(f => ({ ...f, sort: 'name' })),
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
    queryKey: ['pokemon-cards', filters, sets],
    queryFn: ({ pageParam }) => searchPokemonCards(filters, pageParam as number, 20, sets),
    initialPageParam: 1,
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
    staleTime: 5 * 60 * 1000,
  })

  const rawCards = data?.pages.flatMap(p => p.data) ?? []
  const seen = new Set<string>()
  const cards = rawCards.filter(c => seen.has(c.id) ? false : (seen.add(c.id), true))

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
      <h1 className="mb-4 text-xl font-bold text-slate-100 sm:text-2xl">Card Catalogue</h1>

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
              if (showSuggestions) {
                autocompleteRef.current?.handleKeyDown(e)
                if (e.defaultPrevented) return
              }
              if (e.key === 'Enter') { setFilters(f => ({ ...f, name: nameInput || undefined })); setShowSuggestions(false) }
            }}
            placeholder="Search Pokémon cards…"
            className="w-full rounded-lg border border-slate-700/60 bg-slate-900/80 px-3 py-2.5 text-sm text-slate-100 placeholder-slate-500 backdrop-blur-sm focus:border-blue-500 focus:outline-none"
          />
          {showSuggestions && (
            <AutocompleteDropdown
              ref={autocompleteRef}
              query={nameInput}
              onSelect={handleSelectSuggestion}
              onClose={() => setShowSuggestions(false)}
            />
          )}
        </div>
        <button
          onClick={() => { setFilters(f => ({ ...f, name: nameInput || undefined })); setShowSuggestions(false) }}
          className="rounded-lg bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-blue-500 active:scale-95"
        >
          Search
        </button>
      </div>

      {/* Filters */}
      <FilterChips filters={filters} sets={sets} onChange={setFilters} />

      {/* Sort */}
      <SortDropdown filters={filters} onChange={setFilters} />

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
              className="text-blue-400 hover:text-blue-300 underline"
            >
              Clear filters
            </button>
          )}
        </div>
      )}

      {cards.length > 0 && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {cards.map((card, i) => (
            <CardTile
              key={card.id}
              card={card}
              index={i}
              onClick={() => setSelectedCard(card)}
            />
          ))}
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
