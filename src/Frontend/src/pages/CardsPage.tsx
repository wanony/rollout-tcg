import { useState, useEffect, useRef, useContext, useCallback } from 'react'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { motion, AnimatePresence } from 'framer-motion'
import { searchPokemonCards, fetchSets, CardFilters, PokemonCard } from '../api/pokemontcg'
import { TYPE_GLOW } from '../lib/typeColors'
import HoloCard from '../components/HoloCard'
import CardDetailModal from '../components/CardDetailModal'
import FilterChips from '../components/FilterChips'
import SortDropdown from '../components/SortDropdown'
import AutocompleteDropdown, { AutocompleteDropdownHandle } from '../components/AutocompleteDropdown'
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
  const autocompleteRef = useRef<AutocompleteDropdownHandle>(null)
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

  const rawCards = data?.pages.flatMap(p => p.data) ?? []
  const seen = new Set<string>()
  const cards = rawCards.filter(c => seen.has(c.id) ? false : (seen.add(c.id), true))
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
              if (showSuggestions) {
                autocompleteRef.current?.handleKeyDown(e)
                if (e.defaultPrevented) return
              }
              if (e.key === 'Enter') { setFilters(f => ({ ...f, name: nameInput || undefined })); setShowSuggestions(false) }
            }}
            placeholder="Search Pokémon cards…"
            className="w-full rounded-lg border border-slate-700/60 bg-slate-900/80 px-3 py-2.5 text-sm text-slate-100 placeholder-slate-500 backdrop-blur-sm focus:border-violet-500 focus:outline-none"
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
                key={card.id}
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
